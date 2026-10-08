import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import YAML from "yaml";

import { parseFlags } from "../args.js";
import { awStateDir, stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ledgerPath, openLedger, withEpoch } from "../ledger/db.js";
import { acquireTickLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { approveProfile, isApproved, profileDiff } from "./approve.js";
import { explainKey } from "./explain.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "./load.js";
import { PROFILE_SCHEMA_VERSION } from "./schema.js";

const EXAMPLE_DIR = fileURLToPath(new URL("../../profile/examples/generic/", import.meta.url));

export function sanitizeName(s: string): string {
  const out = s.toLowerCase().replace(/[^a-z0-9-]/g, "-").replace(/^[^a-z0-9]+/, "").slice(0, 39);
  return out === "" ? "me" : out;
}

export function requireProfile(deps: Deps, flag?: string): LoadedProfile {
  const root = resolveProfileRoot(deps, flag);
  if (root === null) throw new SindriError("SND-PROFILE-002", "no profile found");
  const r = loadProfile(root);
  if (!r.ok) throw new SindriError("SND-PROFILE-001", `profile at ${root} has ${r.issues.length} issue(s); run \`sindri profile validate\``);
  return r.value;
}

function genericFiles(user: string, host: string): Record<string, string> {
  const profile = fs
    .readFileSync(path.join(EXAMPLE_DIR, "profile.yaml"), "utf8")
    .replace(/^user: me\b/m, `user: ${user}`)
    .replace(/^ {2}active: my-host\b/m, `  active: ${host}`);
  return { "profile.yaml": profile, "repos/example.yaml": fs.readFileSync(path.join(EXAMPLE_DIR, "repos/example.yaml"), "utf8") };
}

export async function ring0Files(deps: Deps, user: string, host: string, include: string[] = ["*"]): Promise<Record<string, string>> {
  const top = await deps.git.run(["rev-parse", "--show-toplevel"], deps.cwd);
  if (!top.ok) throw new SindriError("SND-PROFILE-009", `${deps.cwd} is not inside a git repo`);
  const root = top.stdout.trim();
  if (!fs.existsSync(path.join(root, "docs/superpowers/plans"))) {
    throw new SindriError("SND-PROFILE-008", `${root} has no docs/superpowers/plans; --ring0 is for a repo built from its own plan files`);
  }
  const name = sanitizeName(path.basename(root));
  const email = await deps.git.run(["config", "user.email"], root);
  const trusted = email.ok && email.stdout.trim() !== "" ? [email.stdout.trim()] : [];
  const header = "# Ring-0 profile: this repo builds itself from its plan files (spec §13.3).\n";
  const profile = {
    schemaVersion: PROFILE_SCHEMA_VERSION,
    mode: "shadow",
    user,
    hosts: { active: host },
    tracker: { type: "plan-file", repo: name, glob: "docs/superpowers/plans/*.md", include },
    repos: [name],
    trustedAuthors: trusted,
  };
  const repo = { schemaVersion: PROFILE_SCHEMA_VERSION, name, path: root, defaultBranch: "main", protectedPaths: [".github/**", "config/hooks/**", "config/settings.json"] };
  return { "profile.yaml": header + YAML.stringify(profile), [`repos/${name}.yaml`]: YAML.stringify(repo) };
}

async function init(args: string[], deps: Deps): Promise<CommandResult> {
  const { values } = parseFlags(args, {
    ring0: { type: "boolean" }, plans: { type: "string", multiple: true }, dir: { type: "string" }, force: { type: "boolean" }, json: { type: "boolean" },
  });
  const link = path.join(awStateDir(deps), "profile");
  const dir = values.dir === undefined ? link : path.resolve(deps.cwd, values.dir);
  if (fs.existsSync(path.join(dir, "profile.yaml")) && values.force !== true) {
    throw new SindriError("SND-PROFILE-007", `${path.join(dir, "profile.yaml")} already exists`);
  }
  const user = sanitizeName(deps.env.USER ?? "me");
  const host = deps.system.hostname();
  const files = values.ring0 === true ? await ring0Files(deps, user, host, values.plans ?? ["*"]) : genericFiles(user, host);
  for (const [rel, text] of Object.entries(files)) {
    const target = path.join(dir, rel);
    fs.mkdirSync(path.dirname(target), { recursive: true, mode: 0o700 });
    if (values.force === true) fs.rmSync(target, { force: true });
    // "wx": never write through a symlink or over a file we didn't just remove.
    fs.writeFileSync(target, text, { flag: "wx", mode: 0o600 });
  }
  const notes: string[] = [];
  if (dir !== link) {
    const st = fs.lstatSync(link, { throwIfNoEntry: false });
    if (st === undefined) {
      fs.mkdirSync(path.dirname(link), { recursive: true, mode: 0o700 });
      fs.symlinkSync(dir, link);
      notes.push(`Linked ${link} -> ${dir}`);
    } else if (!st.isSymbolicLink() || path.resolve(path.dirname(link), fs.readlinkSync(link)) !== dir) {
      notes.push(`Note: ${link} points elsewhere; pass --profile ${dir} or set AW_PROFILE_DIR.`);
    }
  }
  const text = [`Wrote ${Object.keys(files).join(", ")} to ${dir} (mode: shadow).`, ...notes, "Next: sindri profile validate, then sindri profile approve."].join("\n");
  return success(text, { dir, files: Object.keys(files), notes }, values.json === true);
}

function validate(args: string[], deps: Deps): CommandResult {
  const { values } = parseFlags(args, { profile: { type: "string" }, json: { type: "boolean" } });
  const json = values.json === true;
  const root = resolveProfileRoot(deps, values.profile);
  if (root === null) throw new SindriError("SND-PROFILE-002", "no profile found");
  const r = loadProfile(root);
  if (r.ok) return success(`Profile valid. (${root}, hash ${r.value.hash.slice(0, 12)})`, { ok: true, root, hash: r.value.hash }, json);
  const details = r.issues.map((i) => `${i.file}${i.keyPath ? `: ${i.keyPath}` : ""}: ${i.message}${i.hint ? ` (fix: ${i.hint})` : ""}`);
  return failure("SND-PROFILE-001", `profile has ${r.issues.length} issue(s):`, json, { details, fix: "edit each listed key, then sindri profile validate" });
}

function explain(args: string[], deps: Deps): CommandResult {
  const { values, positionals } = parseFlags(args, { profile: { type: "string" }, repo: { type: "string" }, json: { type: "boolean" } });
  const key = positionals[0];
  if (key === undefined) throw new SindriError("SND-CLI-002", "usage: sindri profile explain <key> [--repo <name>]");
  const loaded = requireProfile(deps, values.profile);
  if (values.repo !== undefined && !(values.repo in loaded.repos)) throw new SindriError("SND-PROFILE-004", `no repo named ${values.repo}`);
  const r = explainKey(loaded, key, values.repo);
  if (r === null) throw new SindriError("SND-PROFILE-003", `no profile key ${key}`);
  return success(`${r.key} = ${JSON.stringify(r.value)}  (from ${r.source})`, r, values.json === true);
}

function migrate(args: string[], deps: Deps): CommandResult {
  const { values } = parseFlags(args, { profile: { type: "string" }, "dry-run": { type: "boolean" }, json: { type: "boolean" } });
  const root = resolveProfileRoot(deps, values.profile);
  if (root === null) throw new SindriError("SND-PROFILE-002", "no profile found");
  const repoDir = path.join(root, "repos");
  const files = ["profile.yaml", ...(fs.existsSync(repoDir) ? fs.readdirSync(repoDir).map((n) => `repos/${n}`) : [])];
  for (const rel of files) {
    let raw: unknown;
    try {
      raw = YAML.parse(fs.readFileSync(path.join(root, rel), "utf8"));
    } catch {
      throw new SindriError("SND-PROFILE-001", `${rel}: not valid YAML; run \`sindri profile validate\``);
    }
    const v = (raw as { schemaVersion?: unknown } | null)?.schemaVersion;
    if (typeof v === "number" && v > PROFILE_SCHEMA_VERSION) {
      throw new SindriError("SND-PROFILE-005", `${rel} has schemaVersion ${v}; this sindri knows ${PROFILE_SCHEMA_VERSION}`);
    }
    if (v !== PROFILE_SCHEMA_VERSION) throw new SindriError("SND-PROFILE-001", `${rel}: schemaVersion must be ${PROFILE_SCHEMA_VERSION}`);
  }
  const text = `Profile is at schemaVersion ${PROFILE_SCHEMA_VERSION} (current). Nothing to migrate.`;
  return success(text, { schemaVersion: PROFILE_SCHEMA_VERSION, migrations: [] }, values.json === true);
}

async function approve(args: string[], deps: Deps): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { profile: { type: "string" }, json: { type: "boolean" } });
  const json = values.json === true;
  const loaded = requireProfile(deps, values.profile);
  const short = loaded.hash.slice(0, 12);
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    const want = positionals[0];
    if (want === undefined) {
      if (isApproved(db, loaded.hash)) return success(`Profile ${short} is approved.`, { hash: loaded.hash, approved: true }, json);
      const diff = profileDiff(deps, db, loaded);
      const text = [`Profile ${short} is not approved. Changes since the last approval:`, ...diff, "", `To approve: sindri profile approve ${short}`].join("\n");
      return success(text, { hash: loaded.hash, approved: false, diff }, json, 1);
    }
    if (want.length < 12) throw new SindriError("SND-CLI-002", "give at least 12 characters of the profile hash", { fix: `sindri profile approve ${short}` });
    if (!loaded.hash.startsWith(want)) {
      throw new SindriError("SND-PROFILE-006", `${want} does not match the current profile (${short}); it changed since you viewed it`);
    }
    // Spec §8.7: approval is a human verb. An agent can still drive a pty, so this is
    // friction plus intent, not a boundary; the session boundary arrives with step 3a.
    if (!deps.isTTY) throw new SindriError("SND-PROFILE-010", "approving a profile needs an interactive terminal");
    const diff = profileDiff(deps, db, loaded);
    const answer = await deps.prompt(`${diff.join("\n")}\n\nApprove profile ${short}? Type its first 6 characters to confirm: `);
    if (answer.trim() !== loaded.hash.slice(0, 6)) throw new SindriError("SND-PROFILE-011", "approval not confirmed");
    const lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
    if (!lock.ok) throw new SindriError("SND-LOCK-001", lock.detail);
    try {
      withEpoch(db, lock.owner.epoch, () => approveProfile(deps, db, loaded));
    } finally {
      lock.release();
    }
    return success(`Approved profile ${short}. It takes effect on the next run.`, { hash: loaded.hash, approved: true }, json);
  } finally {
    db.close();
  }
}

export const profileCommand: Command = async (args, deps) => {
  const [sub, ...rest] = args;
  const json = rest.includes("--json");
  try {
    switch (sub) {
      case "init":
        return await init(rest, deps);
      case "validate":
        return validate(rest, deps);
      case "explain":
        return explain(rest, deps);
      case "migrate":
        return migrate(rest, deps);
      case "approve":
        return await approve(rest, deps);
      default:
        return failure("SND-CLI-002", `unknown profile subcommand: ${sub ?? "(none)"}; use init, validate, explain, migrate or approve`, json);
    }
  } catch (e) {
    return fromError(e, json);
  }
};
