import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import { ERRORS, SindriError } from "../errors.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import type { ExitCode } from "../output.js";
import { approvedProfile } from "../profile/approve.js";
import { requireProfile } from "../profile/commands.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { installPreCommit, preCommitHook } from "../scrub/commands.js";
import { buildIndex } from "./build.js";
import { embedderOrUnavailable, graphFor } from "./commands.js";
import { indexPath } from "./db.js";
import { mainCheckout, repoAdd } from "./repo-add.js";
import type { Step } from "./setup.js";

export type RepoState =
  | { kind: "outside-git" }
  | { kind: "no-approved-profile"; path: string }
  | { kind: "not-onboarded"; path: string; name?: string }
  | { kind: "onboarded"; path: string; name: string };

const realOrSelf = (p: string): string => {
  try {
    return fs.realpathSync(p);
  } catch {
    return p;
  }
};

function nameFor(loaded: LoadedProfile, candidates: string[]): string | null {
  for (const [name, r] of Object.entries(loaded.repos)) if (candidates.includes(realOrSelf(r.path))) return name;
  return null;
}

// Spec §5.2: hooks never write the ledger. readLedger is query_only: no migration, no lock,
// no -wal/-shm left behind. It reads any schema version (approvals exist since v1), so a
// ledger one version behind or ahead of this build never silently turns the gate off.
// A ledger that can't be read throws: `repo status` exits 2 and the template hook says so.
function readApproved(deps: Deps): LoadedProfile | null {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return null;
  return readLedger(file, (db) => approvedProfile(deps, db));
}

// The repo's top level and its main checkout (mainCheckout: none for a worktree of a bare repo,
// a separate git dir or a submodule, so such a worktree never matches a profile repo by accident).
export async function repoState(deps: Deps, target: string): Promise<RepoState> {
  const r = await deps.git.run(["rev-parse", "--path-format=absolute", "--show-toplevel", "--git-dir", "--git-common-dir"], path.resolve(deps.cwd, target), { timeoutMs: 5_000 });
  if (!r.ok) return { kind: "outside-git" };
  const [top, gitDir, common] = r.stdout.trim().split("\n");
  const repoPath = realOrSelf(top);
  const main = mainCheckout(top, gitDir, common);
  const candidates = main === null ? [repoPath] : [repoPath, main];
  const approved = readApproved(deps);
  if (approved === null) return { kind: "no-approved-profile", path: repoPath };
  const name = nameFor(approved, candidates);
  if (name !== null) return { kind: "onboarded", path: repoPath, name };
  const root = resolveProfileRoot(deps);
  const live = root === null ? null : loadProfile(root);
  const pending = live !== null && live.ok ? nameFor(live.value, candidates) : null;
  return pending === null ? { kind: "not-onboarded", path: repoPath } : { kind: "not-onboarded", path: repoPath, name: pending };
}

// One line or nothing. Silent before any approval: a user who never set up a profile
// is not nagged in every repo (`sindri doctor` covers that).
export function nudgeLine(s: RepoState): string {
  if (s.kind !== "not-onboarded") return "";
  return s.name === undefined
    ? `sindri: ${path.basename(s.path)} is not onboarded (no secret scan, shape signals or index); run: sindri repo onboard`
    : `sindri: ${s.name} is waiting for approval; run: sindri repo onboard`;
}

export const templateDir = (deps: Deps): string => path.join(stateDir(deps), "git-template");
const globalEnv = (deps: Deps): Record<string, string> | undefined =>
  deps.env.GIT_CONFIG_GLOBAL === undefined ? undefined : { GIT_CONFIG_GLOBAL: deps.env.GIT_CONFIG_GLOBAL };
const expandHome = (deps: Deps, p: string): string => path.resolve(deps.home, p.replace(/^~(?=\/|$)/, deps.home));

// init.templateDir only: never core.hooksPath (it would override every repo's own hooks).
// The hook file lives in sindri's own state dir; another tool's template dir is never written.
export async function installTemplate(deps: Deps): Promise<Step> {
  const dir = templateDir(deps);
  const hook = path.join(dir, "hooks", "pre-commit");
  const text = preCommitHook(deps.env.SINDRI_BIN ?? "sindri", { template: true });
  fs.mkdirSync(path.dirname(hook), { recursive: true, mode: 0o700 });
  const changed = !fs.existsSync(hook) || fs.readFileSync(hook, "utf8") !== text;
  if (changed) fs.writeFileSync(hook, text);
  fs.chmodSync(hook, 0o755);
  const env = globalEnv(deps);
  const cur = await deps.git.run(["config", "--global", "--get", "init.templateDir"], deps.home, { env });
  if (!cur.ok && cur.code !== 1) throw new SindriError("SND-SCRUB-006", `could not read init.templateDir: ${cur.stderr.trim()}`);
  const current = cur.ok ? cur.stdout.trim() : "";
  if (current !== "" && realOrSelf(expandHome(deps, current)) !== realOrSelf(dir)) {
    throw new SindriError("SND-SCRUB-006", `init.templateDir is already set to ${current}; sindri does not write into another template dir`, {
      fix: `cp ${hook} ${path.join(current, "hooks", "pre-commit")} (if that dir has no pre-commit hook), or git config --global --unset init.templateDir and rerun`,
    });
  }
  if (current === "") {
    const set = await deps.git.run(["config", "--global", "init.templateDir", dir], deps.home, { env });
    if (!set.ok) throw new SindriError("SND-SCRUB-006", `could not set init.templateDir: ${set.stderr.trim()}`);
    return { name: "template", status: "done", detail: `init.templateDir = ${dir}; new clones and git init get the hook (a no-op until onboarded)` };
  }
  return { name: "template", status: changed ? "done" : "ok", detail: `init.templateDir = ${dir}` };
}

function failStep(name: string, e: SindriError): Step {
  return { name, status: "fail", detail: `${e.code} ${e.message}`, fix: e.fix ?? ERRORS[e.code].fix };
}

// Each step reports like `index setup`. Approval is never done here: the profile change waits for a
// human at a terminal (spec §8.7, invariant 10), and the exit code (1) tells the caller so.
export async function onboard(deps: Deps, target: string, o: { name?: string; build: boolean }): Promise<{ name: string; steps: Step[]; exitCode: ExitCode }> {
  const added = await repoAdd(deps, target, o.name);
  const steps: Step[] = [{ name: "repo-add", status: added.added ? "done" : "ok", detail: `${added.name} (${added.path}) is in the live profile` }];
  const approved = readApproved(deps);
  const entry = approved?.repos[added.name];
  if (approved === null || entry === undefined || realOrSelf(entry.path) !== added.path) {
    const short = requireProfile(deps).hash.slice(0, 12);
    steps.push(
      { name: "approval", status: "warn", detail: "the profile changed; approval is pending", fix: `sindri profile approve (review the diff), then at a terminal: sindri profile approve ${short}; then rerun sindri repo onboard` },
      { name: "pre-commit", status: "skip", detail: "needs approval" },
      { name: "index-build", status: "skip", detail: "needs approval" },
    );
    return { name: added.name, steps, exitCode: 1 };
  }
  steps.push({ name: "approval", status: "ok", detail: `in approved profile ${approved.hash.slice(0, 12)}` });
  try {
    const h = await installPreCommit(deps, added.path);
    steps.push({ name: "pre-commit", status: h.changed ? "done" : "ok", detail: h.hook });
  } catch (e) {
    if (!(e instanceof SindriError)) throw e;
    steps.push(failStep("pre-commit", e));
  }
  if (!o.build) {
    steps.push({ name: "index-build", status: "skip", detail: "--no-build" });
  } else if (fs.existsSync(indexPath(deps, added.name))) {
    steps.push({ name: "index-build", status: "ok", detail: "already built; the nightly build refreshes it" });
  } else {
    deps.log(`building ${added.name}; this takes the heavy-job lock`);
    try {
      // One try at the lock: onboarding never waits behind another heavy job (the nightly build picks it up).
      const r = await buildIndex(deps, approved, added.name, { full: false, mirror: true, lockTimeoutMs: 0 }, { embedder: embedderOrUnavailable(approved, deps.io), graph: graphFor(approved, deps, deps.io) });
      steps.push({ name: "index-build", status: "done", detail: `${r.files.indexed} files, ${r.symbols} symbols` });
    } catch (e) {
      if (!(e instanceof SindriError) || e.code !== "SND-INDEX-001") throw e;
      steps.push({ name: "index-build", status: "warn", detail: e.message, fix: `sindri index build --repo ${added.name}, or let the nightly build do it` });
    }
  }
  const exitCode: ExitCode = steps.some((s) => s.status === "fail") ? 2 : steps.some((s) => s.status === "warn") ? 1 : 0;
  return { name: added.name, steps, exitCode };
}
