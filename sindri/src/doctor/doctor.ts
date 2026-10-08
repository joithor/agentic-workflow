import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { LEDGER_SCHEMA_VERSION, ledgerPath, readLedger, schemaVersion } from "../ledger/db.js";
import { inspectLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { fromError, success, type ExitCode } from "../output.js";
import { approvalProblem, approvalState, type ApprovalState } from "../profile/approve.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { hookBinary, preCommitPath, PRE_COMMIT_MARKER } from "../scrub/commands.js";

function isExecutable(p: string): boolean {
  try {
    fs.accessSync(p, fs.constants.X_OK);
    return true;
  } catch {
    return false;
  }
}

export interface Check {
  name: string;
  status: "ok" | "warn" | "fail";
  detail: string;
  fix?: string;
}

const INIT_FIX = "sindri profile init --ring0 (or sindri profile init)";

function stateDirCheck(deps: Deps): Check {
  const dir = stateDir(deps);
  if (!fs.existsSync(dir)) return { name: "state-dir", status: "warn", detail: "not created yet", fix: INIT_FIX };
  const local = deps.system.isLocalDisk(dir);
  if (local === false) return { name: "state-dir", status: "fail", detail: "on a network filesystem", fix: "set AW_STATE_DIR to a local path (spec §9.1)" };
  const mode = fs.statSync(dir).mode & 0o777;
  const problems = [...(mode === 0o700 ? [] : [`mode ${mode.toString(8)} (want 700)`]), ...(local === null ? ["can't tell if it is on local disk"] : [])];
  if (problems.length === 0) return { name: "state-dir", status: "ok", detail: dir };
  return { name: "state-dir", status: "warn", detail: problems.join("; "), fix: `chmod 700 ${dir}` };
}

function ledgerCheck(deps: Deps): Check {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return { name: "ledger", status: "ok", detail: "no ledger yet" };
  try {
    // Doctor is read-only: readLedger never migrates, backs up, chmods or leaves files.
    const v = readLedger(file, schemaVersion);
    if (v > LEDGER_SCHEMA_VERSION) throw new SindriError("SND-LEDGER-001", `ledger schema v${v} is newer than this sindri (v${LEDGER_SCHEMA_VERSION})`);
    return { name: "ledger", status: "ok", detail: `schema v${v} of ${LEDGER_SCHEMA_VERSION}` };
  } catch (e) {
    const code = e instanceof SindriError ? `${e.code} ` : "";
    return { name: "ledger", status: "fail", detail: `${code}${(e as Error).message}`, fix: "upgrade sindri: scripts/install-sindri.sh from the latest main" };
  }
}

function lockCheck(deps: Deps): Check {
  const dir = stateDir(deps);
  const l = inspectLock(dir, deps.system, deps.now);
  const who = l.owner === null ? "an unreadable owner" : `${l.owner.host}/${l.owner.pid}`;
  const state = { free: "free", held: l.owner === null ? `held by ${who}` : `held by ${who} since ${l.owner.startedAt}`, stale: `stale lock from ${who}; the next run takes it over` }[l.state];
  const detail = l.leftovers.length > 0 ? `${state}; leftovers: ${l.leftovers.join(", ")}` : state;
  // A stale lock alone needs nothing: the next run takes it over. Leftover dirs
  // from a crashed takeover need removing; that is safe.
  if (l.leftovers.length === 0) return { name: "lock", status: "ok", detail };
  return { name: "lock", status: "warn", detail, fix: `remove ${l.leftovers.map((n) => path.join(dir, n)).join(" ")} (left by a crashed run)` };
}

function approvedCheck(deps: Deps, loaded: LoadedProfile): { check: Check; state: ApprovalState | null } {
  const file = ledgerPath(stateDir(deps));
  let state: ApprovalState = { kind: "never-approved" };
  if (fs.existsSync(file)) {
    try {
      state = readLedger(file, (db) => approvalState(deps, db, loaded.hash));
    } catch {
      // The ledger check reports why the ledger can't be read.
      const check: Check = { name: "profile-approved", status: "warn", detail: "can't read approvals from the ledger (see the ledger check)", fix: "fix the ledger check first, then rerun sindri doctor" };
      return { check, state: null };
    }
  }
  if (state.kind === "approved") return { check: { name: "profile-approved", status: "ok", detail: loaded.hash.slice(0, 12) }, state };
  return { check: { name: "profile-approved", status: "warn", detail: approvalProblem(state, loaded.hash), fix: "sindri profile approve" }, state };
}

async function profileChecks(deps: Deps, loaded: LoadedProfile): Promise<Check[]> {
  const { check, state } = approvedCheck(deps, loaded);
  // Spec §8.7: runs use the approved snapshot, so the checks below read it when there
  // is one; the live profile only when nothing usable is approved yet.
  const snapshot = state?.kind === "approved" || state?.kind === "changed-since-approval" ? state.approved : null;
  const used = snapshot ?? loaded;
  const host = deps.system.hostname();
  const active = used.profile.hosts.active;
  const b = used.profile.budget;
  const out: Check[] = [
    check,
    {
      name: "profile-in-use",
      status: "ok",
      detail: snapshot === null ? "live profile (nothing approved yet); the checks below read it" : `approved profile ${snapshot.hash.slice(0, 12)}; the checks below read it`,
    },
    active === host
      ? { name: "active-host", status: "ok", detail: host }
      : { name: "active-host", status: "warn", detail: `this host is ${host}; hosts.active is ${active}`, fix: "edit hosts.active, then sindri profile approve" },
    {
      name: "budget",
      status: "ok",
      detail: b.perItem === undefined && b.perDay === undefined ? "unset (not enforced before rollout step 3a)" : `perItem ${b.perItem ?? "unset"}, perDay ${b.perDay ?? "unset"} tokens`,
    },
  ];
  for (const [name, repo] of Object.entries(used.repos)) {
    const hook = await preCommitPath(deps.git, repo.path);
    const text = hook !== null && fs.existsSync(hook) ? fs.readFileSync(hook, "utf8") : "";
    const bin = text.includes(PRE_COMMIT_MARKER) ? hookBinary(text) : null;
    const fix = `sindri scrub --install-pre-commit --repo ${repo.path}`;
    if (bin === null) out.push({ name: `pre-commit:${name}`, status: "warn", detail: "secret-scan hook not installed", fix });
    else if (path.isAbsolute(bin) && !isExecutable(bin)) out.push({ name: `pre-commit:${name}`, status: "warn", detail: `hook calls ${bin}, which is missing, so every commit is refused`, fix: `scripts/install-sindri.sh, then ${fix}` });
    else out.push({ name: `pre-commit:${name}`, status: "ok", detail: `${hook} → ${bin}` });
  }
  return out;
}

export async function runChecks(deps: Deps, nodeVersion: string = process.versions.node): Promise<Check[]> {
  const [major, minor] = nodeVersion.split(".").map(Number);
  const checks: Check[] = [
    major > 20 || (major === 20 && minor >= 11) ? { name: "node", status: "ok", detail: nodeVersion } : { name: "node", status: "fail", detail: `${nodeVersion} (need >= 20.11)`, fix: "install Node 20.11 or newer" },
    stateDirCheck(deps),
    { name: "boot-id", status: "ok", detail: deps.system.bootId() === null ? "unreadable; stale-lock checks use pids only" : "readable" },
    ledgerCheck(deps),
    lockCheck(deps),
  ];
  const root = resolveProfileRoot(deps);
  if (root === null) return [...checks, { name: "profile", status: "warn", detail: "no profile", fix: INIT_FIX }];
  const r = loadProfile(root);
  if (!r.ok) return [...checks, { name: "profile", status: "fail", detail: `${r.issues.length} issue(s) in ${root}`, fix: "sindri profile validate" }];
  return [...checks, { name: "profile", status: "ok", detail: root }, ...(await profileChecks(deps, r.value))];
}

export const doctorCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  try {
    parseFlags(args, { json: { type: "boolean" } });
    const checks = await runChecks(deps);
    const exitCode: ExitCode = checks.some((c) => c.status === "fail") ? 2 : checks.some((c) => c.status === "warn") ? 1 : 0;
    const width = Math.max(...checks.map((c) => c.name.length));
    const text = checks
      .map((c) => `${c.status.padEnd(4)} ${c.name.padEnd(width)}  ${c.detail}${c.status !== "ok" && c.fix !== undefined ? `\n     fix: ${c.fix}` : ""}`)
      .join("\n");
    return success(text, checks, json, exitCode);
  } catch (e) {
    return fromError(e, json);
  }
};
