import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import YAML from "yaml";
import type { ZodError } from "zod";

import { awStateDir, type Deps } from "../deps.js";
import { compileExtraPatterns, makeScrubber } from "../scrub/scrub.js";
import { PROFILE_SCHEMA_VERSION, ProfileSchema, RepoSchema, type Profile, type RepoConfig } from "./schema.js";

export interface ProfileIssue {
  file: string;
  keyPath: string;
  message: string;
  hint?: string;
  // Set when the issue has its own error code (a profile for a newer sindri).
  code?: "SND-PROFILE-005";
}

export interface LoadedProfile {
  root: string;
  profile: Profile;
  repos: Record<string, RepoConfig>;
  raw: { profile: unknown; repos: Record<string, unknown> };
  files: string[];
  // The exact bytes that were parsed and validated; the hash and any approval
  // snapshot are made from these, never from a second read (no TOCTOU).
  bytes: Record<string, Buffer>;
  hash: string;
}

export function resolveProfileRoot(deps: Deps, flag?: string): string | null {
  if (flag !== undefined) return path.resolve(deps.cwd, flag);
  if (deps.env.AW_PROFILE_DIR) return deps.env.AW_PROFILE_DIR;
  const link = path.join(awStateDir(deps), "profile");
  return fs.existsSync(link) ? link : null;
}

function readYaml(root: string, rel: string, issues: ProfileIssue[], bytes: Record<string, Buffer>): unknown {
  let buf: Buffer;
  try {
    buf = fs.readFileSync(path.join(root, rel));
  } catch {
    issues.push({ file: rel, keyPath: "", message: "file not found", hint: "sindri profile init" });
    return undefined;
  }
  bytes[rel] = buf;
  const doc = YAML.parseDocument(buf.toString("utf8"));
  for (const e of doc.errors) issues.push({ file: rel, keyPath: "", message: `YAML: ${e.message.split("\n")[0]}` });
  return doc.errors.length > 0 ? undefined : doc.toJS();
}

const scrubber = makeScrubber();

// Zod messages can quote the offending value ("..., received 'ghp_...'") or key.
// Drop the quoted value and scrub the rest, so validate never prints a secret.
function zodIssues(file: string, error: ZodError): ProfileIssue[] {
  return error.issues.map((i) => {
    const message = scrubber.scrub(i.message.replace(/, received '[\s\S]*'$/, "")).text;
    const issue: ProfileIssue = { file, keyPath: scrubber.scrub(i.path.join(".")).text, message };
    return i.code === "unrecognized_keys" ? { ...issue, hint: "remove the key or fix its spelling" } : issue;
  });
}

// A profile holds pointers (env:, file:, keychain:, op:), never secret values.
function secretValueIssues(file: string, value: unknown, keyPath: string[] = []): ProfileIssue[] {
  if (typeof value === "string") {
    const hit = scrubber.find(value)[0];
    if (hit === undefined) return [];
    return [{
      file,
      keyPath: scrubber.scrub(keyPath.join(".")).text,
      message: `value looks like a secret (${hit.kind})`,
      hint: "store it elsewhere and use a pointer: env:NAME, file:/path, keychain:service/account or op:vault/item",
    }];
  }
  if (Array.isArray(value)) return value.flatMap((v, i) => secretValueIssues(file, v, [...keyPath, String(i)]));
  if (value !== null && typeof value === "object") {
    return Object.entries(value).flatMap(([k, v]) => secretValueIssues(file, v, [...keyPath, k]));
  }
  return [];
}

// Secrets outside parsed values (a YAML comment such as "# old token: ...") would
// otherwise be printed by approve's diff and copied into the snapshot.
function rawSecretIssues(file: string, buf: Buffer): ProfileIssue[] {
  const text = buf.toString("utf8");
  return scrubber.find(text).map((hit) => ({
    file,
    keyPath: "",
    message: `line ${text.slice(0, hit.start).split("\n").length} looks like a secret (${hit.kind})`,
    hint: "remove it (comments too); use a pointer such as env:NAME for real values",
  }));
}

// A file for a newer sindri fails fast with its own code, before strict parsing
// reports every new key as unrecognized.
function newerIssues(files: [string, unknown][]): ProfileIssue[] {
  return files.flatMap(([file, raw]) => {
    const v = raw !== null && typeof raw === "object" ? (raw as { schemaVersion?: unknown }).schemaVersion : undefined;
    if (typeof v !== "number" || v <= PROFILE_SCHEMA_VERSION) return [];
    return [{ file, keyPath: "schemaVersion", message: `schemaVersion ${v} is newer than this sindri knows (${PROFILE_SCHEMA_VERSION})`, code: "SND-PROFILE-005" as const }];
  });
}

function hashOf(files: string[], bytes: Record<string, Buffer>): string {
  const h = createHash("sha256");
  for (const rel of files) h.update(rel).update("\0").update(bytes[rel]).update("\0");
  return h.digest("hex");
}

export function profileHash(root: string, files: string[]): string {
  return hashOf(files, Object.fromEntries(files.map((rel) => [rel, fs.readFileSync(path.join(root, rel))])));
}

function crossCheck(p: Profile, rawRepos: Record<string, unknown>): ProfileIssue[] {
  const issues: ProfileIssue[] = [];
  for (const repo of p.repos) {
    if (!Object.hasOwn(rawRepos, repo)) issues.push({ file: "profile.yaml", keyPath: "repos", message: `repo "${repo}" has no file repos/${repo}.yaml` });
  }
  for (const repo of Object.keys(rawRepos)) {
    if (!p.repos.includes(repo)) {
      issues.push({ file: `repos/${repo}.yaml`, keyPath: "", message: "repo file is not listed in profile.yaml repos", hint: `add "${repo}" to repos` });
    }
  }
  if (!p.repos.includes(p.tracker.repo)) {
    issues.push({ file: "profile.yaml", keyPath: "tracker.repo", message: `tracker.repo "${p.tracker.repo}" is not in repos` });
  }
  try {
    compileExtraPatterns(p.scrub.extraPatterns);
  } catch (e) {
    // A regex that doesn't compile is quoted in V8's message: scrub it.
    issues.push({ file: "profile.yaml", keyPath: "scrub.extraPatterns", message: scrubber.scrub((e as Error).message).text });
  }
  return issues;
}

export function loadProfile(root: string): { ok: true; value: LoadedProfile } | { ok: false; issues: ProfileIssue[] } {
  const issues: ProfileIssue[] = [];
  const bytes: Record<string, Buffer> = {};
  const rawProfile = readYaml(root, "profile.yaml", issues, bytes);
  const repoDir = path.join(root, "repos");
  const repoFiles = fs.existsSync(repoDir) ? fs.readdirSync(repoDir).filter((n) => n.endsWith(".yaml")).sort() : [];
  const rawRepos: Record<string, unknown> = {};
  for (const f of repoFiles) rawRepos[f.slice(0, -".yaml".length)] = readYaml(root, `repos/${f}`, issues, bytes);
  if (issues.length > 0) return { ok: false, issues };
  const newer = newerIssues([["profile.yaml", rawProfile], ...Object.entries(rawRepos).map(([repo, raw]): [string, unknown] => [`repos/${repo}.yaml`, raw])]);
  if (newer.length > 0) return { ok: false, issues: newer };

  const parsed = ProfileSchema.safeParse(rawProfile);
  if (!parsed.success) issues.push(...zodIssues("profile.yaml", parsed.error));
  const repos: Record<string, RepoConfig> = {};
  for (const [repo, raw] of Object.entries(rawRepos)) {
    const r = RepoSchema.safeParse(raw);
    if (!r.success) {
      issues.push(...zodIssues(`repos/${repo}.yaml`, r.error));
    } else if (r.data.name !== repo) {
      issues.push({ file: `repos/${repo}.yaml`, keyPath: "name", message: `name "${r.data.name}" must match the file name "${repo}"` });
    } else {
      repos[repo] = r.data;
    }
  }
  for (const [file, raw] of [["profile.yaml", rawProfile], ...Object.entries(rawRepos).map(([repo, r]): [string, unknown] => [`repos/${repo}.yaml`, r])] as [string, unknown][]) {
    const valueIssues = secretValueIssues(file, raw);
    issues.push(...(valueIssues.length > 0 ? valueIssues : rawSecretIssues(file, bytes[file])));
  }
  if (!parsed.success) return { ok: false, issues };
  issues.push(...crossCheck(parsed.data, rawRepos));
  if (issues.length > 0) return { ok: false, issues };

  const files = ["profile.yaml", ...repoFiles.map((f) => `repos/${f}`)];
  return {
    ok: true,
    value: { root, profile: parsed.data, repos, raw: { profile: rawProfile, repos: rawRepos }, files, bytes, hash: hashOf(files, bytes) },
  };
}
