import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import type { GitRunner } from "../git.js";
import type { Ledger } from "../ledger/db.js";

export type ArtifactKind = "skill" | "hook" | "package" | "installer" | "rule" | "doc" | "mod" | "pack-pin" | "prompt";

export interface Artifact {
  id: string;
  kind: ArtifactKind;
  paths: string[];
  root: string | null;
  hash: string;
  protected: boolean;
  suite: { argv: string[]; cwd: string } | null;
}

// Spec §7.7 protected modules. Prefixes end with "/" or "-"; everything else is an exact path. Lower case.
export const PROTECTED_PATHS: readonly string[] = [
  "config/hooks/block-destructive.sh", "config/hooks/block-push-main.sh", "config/hooks/detect-secrets.sh", "config/hooks/external-write-guard.sh",
  "config/hooks/adapters/", "config/lib/", "config/settings.json",
  "providers/", "setup.sh", "scripts/install-",
  "sindri/src/scrub/", "sindri/src/gate/", "sindri/src/scope/model.ts", "sindri/src/secrets.ts", "sindri/src/lock/", "sindri/src/profile/approve.ts", "sindri/src/ledger/db.ts",
  "agents.md", "claude.md", ".claude/", ".cursor/", ".agents/rules/", ".github/", "skills/_shared/", "planning/testing.md",
];

// Invariant 11: the machinery that judges a proposal. Edits to it never self-adopt.
export const EVAL_MACHINERY: readonly string[] = ["sindri/src/evolve/", "sindri/src/scope/map.ts", "sindri/src/scope/gather.ts", "scripts/sync-rules.sh"];

const TEST_PATH = /(^|\/)(tests?|__tests__)\//;
const TEST_FILE = /\.(test|spec)\.[a-z]+$/;
const EVAL_FILE = /(^|\/)(vitest\.(config|workspace)\.[a-z]+|tsconfig\.test\.json|package\.json|package-lock\.json|\.npmrc)$/;

// A repo-relative path a model may name: letters, digits, . _ - /, no .., no leading /, at most 200 characters.
export function normalizeRepoPath(p: string): string | null {
  if (p.length === 0 || p.length > 200 || !/^[A-Za-z0-9._/-]+$/.test(p) || p.startsWith("/") || p.split("/").includes("..")) return null;
  const n = path.posix.normalize(p);
  return n === "." || n.endsWith("/") ? null : n;
}

export const escapeRe = (s: string): string => s.replace(/[.+?^${}()|[\]\\]/g, "\\$&");

export function globMatch(glob: string, p: string): boolean {
  const re = glob.split("**").map((part) => part.split("*").map(escapeRe).join("[^/]*")).join(".*");
  return new RegExp(`^${re}$`, "i").test(p);
}

const matches = (list: readonly string[], lower: string): boolean => list.some((x) => (x.endsWith("/") || x.endsWith("-") ? lower.startsWith(x) : lower === x));

// Compared after normalizing and lower-casing (macOS is case-insensitive). An invalid path counts as machinery: fail closed.
export function isEvalMachinery(p: string): boolean {
  const n = normalizeRepoPath(p);
  if (n === null) return true;
  const lower = n.toLowerCase();
  return matches(EVAL_MACHINERY, lower) || TEST_PATH.test(lower) || TEST_FILE.test(lower) || EVAL_FILE.test(lower);
}

export function isProtectedPath(p: string, extra: readonly string[] = []): boolean {
  const n = normalizeRepoPath(p);
  if (n === null) return true;
  return isEvalMachinery(n) || matches(PROTECTED_PATHS, n.toLowerCase()) || extra.some((g) => globMatch(g, n));
}

// The paths `discover` runs as an artifact's suite, whether or not the artifact has one yet: a PR must not
// supply the test that judges its own change.
const SUITE_PATH = /^(config\/hooks\/tests\/[^/]+\.test\.sh|config\/lib\/tests\/[^/]+\.test\.sh|scripts\/tests\/install-[^/]+\.test\.sh|providers\/tests\/install\.test\.sh)$/;
const stemOf = (p: string): string => p.slice(0, p.length - path.posix.extname(p).length);

// A test file the change ADDS can't weaken the existing suite, so it needn't wait for the owner (invariant 11 holds).
// Only a plain *.test.* / *.spec.* file qualifies, and only when it shadows no tracked file (a new helpers.js would
// beat helpers.ts), is not a `heavy/` test (those run the real model), and cannot become an artifact's suite.
export function isAddedTestAllowed(p: string, tracked: readonly string[], extra: readonly string[] = []): boolean {
  const n = normalizeRepoPath(p);
  if (n === null) return false;
  const lower = n.toLowerCase();
  if (!TEST_FILE.test(lower) || /(^|\/)heavy\//.test(lower) || SUITE_PATH.test(lower)) return false;
  if (matches(EVAL_MACHINERY, lower) || EVAL_FILE.test(lower) || matches(PROTECTED_PATHS, lower) || extra.some((g) => globMatch(g, n))) return false;
  const stem = stemOf(lower);
  return !tracked.some((t) => stemOf(t.toLowerCase()) === stem);
}

// Kinds whose identity is the protected file itself. A package or skill is never protected as a whole.
const WHOLE_ARTIFACT_KINDS: readonly ArtifactKind[] = ["hook", "installer", "rule"];
const isSuiteFile = (p: string): boolean => TEST_PATH.test(p) || TEST_FILE.test(p);
const protectedFor = (kind: ArtifactKind, paths: string[], extra: readonly string[]): boolean =>
  WHOLE_ARTIFACT_KINDS.includes(kind) && paths.filter((p) => !isSuiteFile(p)).some((p) => isProtectedPath(p, extra));

function hashFiles(root: string, files: string[], extra = ""): string {
  const h = createHash("sha256").update(extra);
  for (const f of [...files].sort()) h.update(f).update("\0").update(fs.readFileSync(path.join(root, f))).update("\0");
  return h.digest("hex");
}

const isRegular = (root: string, rel: string): boolean => fs.lstatSync(path.join(root, rel), { throwIfNoEntry: false })?.isFile() === true;

export async function discover(
  git: GitRunner, repoPath: string, prompts: readonly { id: string; text: string }[], extraProtected: readonly string[] = [],
): Promise<Artifact[]> {
  const ls = await git.run(["ls-files", "-z"], repoPath);
  if (!ls.ok) throw new SindriError("SND-EVOLVE-001", `${repoPath} is not a readable git repository`);
  const tracked = ls.stdout.split("\0").filter((p) => p !== "" && isRegular(repoPath, p));
  const has = (p: string): boolean => tracked.includes(p);
  const under = (prefix: string): string[] => tracked.filter((p) => p.startsWith(prefix));
  const out: Artifact[] = [];
  const add = (id: string, kind: ArtifactKind, paths: string[], root: string | null, suite: Artifact["suite"]): void => {
    out.push({ id, kind, paths, root, hash: hashFiles(repoPath, paths), protected: protectedFor(kind, paths, extraProtected), suite });
  };

  const skillNames = [...new Set(under("skills/").map((p) => p.split("/")[1]))].filter((n) => n !== "_shared" && has(`skills/${n}/SKILL.md`));
  for (const n of skillNames) add(`skill:${n}`, "skill", under(`skills/${n}/`), `skills/${n}/`, has(`skills/${n}/package.json`) ? { argv: ["npm", "test"], cwd: `skills/${n}` } : null);
  if (under("skills/_shared/").length > 0) add("doc:skills-shared", "doc", under("skills/_shared/"), null, null);
  const preambles = tracked.filter((p) => /^skills\/_[^/]+\.md$/.test(p));
  if (preambles.length > 0) add("doc:skills-preamble", "doc", preambles, null, null);
  if (has("bootstrap/SKILL.md")) add("skill:bootstrap", "skill", under("bootstrap/"), "bootstrap/", null);

  // paths[0] of a hook is always the hook script; its test, when there is one, follows.
  for (const hook of tracked.filter((p) => /^config\/hooks\/[^/]+\.sh$/.test(p))) {
    const name = path.basename(hook, ".sh");
    const test = [`config/hooks/tests/${name}.test.sh`, `config/lib/tests/${name}.test.sh`].find(has);
    add(`hook:${name}`, "hook", test === undefined ? [hook] : [hook, test], null, test === undefined ? null : { argv: ["bash", test], cwd: "." });
  }

  const adapters = under("config/hooks/adapters/");
  if (adapters.length > 0) {
    const tests = ["config/hooks/tests/codex-adapter.test.sh", "config/hooks/tests/cursor-adapter.test.sh"].filter(has);
    add("hook:adapters", "hook", [...adapters, ...tests], "config/hooks/adapters/", tests.length === 0 ? null : { argv: ["bash", "-c", 'for t in "$@"; do bash "$t" || exit 1; done', "adapters", ...tests], cwd: "." });
  }

  for (const pkg of ["judge", "scorer", "mcp-bridge", "sindri"].filter((p) => has(`${p}/package.json`))) {
    add(`package:${pkg}`, "package", under(`${pkg}/`), `${pkg}/`, { argv: ["npm", "test"], cwd: pkg });
  }

  const installerSuite = has("providers/tests/install.test.sh") ? { argv: ["bash", "providers/tests/install.test.sh"], cwd: "." } : null;
  for (const inst of tracked.filter((p) => /^providers\/[^/]+\/install\.sh$/.test(p))) add(`installer:providers-${inst.split("/")[1]}`, "installer", [inst], null, installerSuite);
  if (has("setup.sh")) add("installer:setup", "installer", ["setup.sh"], null, { argv: ["./setup.sh", "--providers", "claude,codex,cursor", "--dry-run"], cwd: "." });

  for (const script of tracked.filter((p) => /^scripts\/install-[^/]+\.sh$/.test(p))) {
    const name = path.basename(script, ".sh").slice("install-".length);
    const test = [`config/lib/tests/install-${name}.test.sh`, `scripts/tests/install-${name}.test.sh`].find(has);
    add(`installer:${name}`, "installer", [script], null, test === undefined ? null : { argv: ["bash", test], cwd: "." });
  }

  const ruleSuite = has("scripts/sync-rules.sh") ? { argv: ["scripts/sync-rules.sh", "--check"], cwd: "." } : null;
  for (const rule of tracked.filter((p) => /^\.agents\/rules\/[^/]+\.md$/.test(p))) add(`rule:${path.basename(rule, ".md")}`, "rule", [rule], null, ruleSuite);
  for (const doc of tracked.filter((p) => /^planning\/[^/]+\.md$/.test(p))) add(`doc:${path.basename(doc, ".md").toLowerCase()}`, "doc", [doc], null, null);

  for (const mod of [...new Set(under("mods/").filter((p) => p.split("/").length > 2).map((p) => p.split("/")[1]))]) add(`mod:${mod}`, "mod", under(`mods/${mod}/`), `mods/${mod}/`, { argv: ["bash", "-c", 'claude plugin validate "$1" && claude plugin test "$1"', "mod", `mods/${mod}`], cwd: "." });
  if (has("EXTERNAL_PINS.env")) add("pack-pin:external", "pack-pin", ["EXTERNAL_PINS.env"], null, null);

  for (const p of prompts) out.push({ id: `prompt:${p.id}`, kind: "prompt", paths: [], root: null, hash: hashFiles(repoPath, [], p.text), protected: false, suite: null });
  const ids = new Set<string>();
  for (const a of out) {
    if (ids.has(a.id)) throw new SindriError("SND-EVOLVE-001", `duplicate artifact id ${a.id}`);
    ids.add(a.id);
  }
  return out.sort((a, b) => (a.id < b.id ? -1 : 1)); // code-point order: stable across locales; ids are unique
}

export function saveRegistry(db: Ledger, artifacts: Artifact[], epoch: number, now: Date): { added: number; changed: number; removed: number } {
  const ts = now.toISOString();
  const counts = { added: 0, changed: 0, removed: 0 };
  const known = new Map((db.prepare("SELECT id, hash FROM artifacts WHERE removed_at IS NULL").all() as { id: string; hash: string }[]).map((r) => [r.id, r.hash]));
  db.transaction(() => {
    for (const a of artifacts) {
      const prev = known.get(a.id);
      if (prev === undefined) counts.added++;
      else if (prev !== a.hash) counts.changed++;
      db.prepare(
        `INSERT INTO artifacts (id, kind, paths, root, hash, protected, suite, first_seen, changed_at, removed_at, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, NULL, ?)
         ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, paths = excluded.paths, root = excluded.root, protected = excluded.protected, suite = excluded.suite,
           removed_at = NULL, epoch = excluded.epoch,
           changed_at = CASE WHEN artifacts.hash = excluded.hash THEN artifacts.changed_at ELSE excluded.changed_at END, hash = excluded.hash`,
      ).run(a.id, a.kind, JSON.stringify(a.paths), a.root, a.hash, a.protected ? 1 : 0, a.suite === null ? null : JSON.stringify(a.suite), ts, ts, epoch);
    }
    const live = new Set(artifacts.map((a) => a.id));
    for (const id of known.keys()) {
      if (!live.has(id)) {
        db.prepare("UPDATE artifacts SET removed_at = ?, epoch = ? WHERE id = ?").run(ts, epoch, id);
        counts.removed++;
      }
    }
  })();
  return counts;
}

export function loadRegistry(db: Ledger): Artifact[] {
  const rows = db.prepare("SELECT id, kind, paths, root, hash, protected, suite FROM artifacts WHERE removed_at IS NULL ORDER BY id").all() as {
    id: string; kind: ArtifactKind; paths: string; root: string | null; hash: string; protected: number; suite: string | null;
  }[];
  return rows.map((r) => ({
    id: r.id, kind: r.kind, paths: JSON.parse(r.paths) as string[], root: r.root, hash: r.hash, protected: r.protected === 1,
    suite: r.suite === null ? null : (JSON.parse(r.suite) as Artifact["suite"]),
  }));
}
