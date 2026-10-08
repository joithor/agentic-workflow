import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { Ledger } from "../ledger/db.js";
import { loadProfile, profileHash, type LoadedProfile } from "./load.js";

export function snapshotDir(deps: Deps, hash: string): string {
  return path.join(stateDir(deps), "profile-approved", hash);
}

// The latest approval is the one inserted last: approveProfile re-inserts on every
// approval, so a rollback moves to the end whatever the wall clock says.
export function lastApproved(db: Ledger): { hash: string; approved_at: string } | null {
  const row = db.prepare("SELECT hash, approved_at FROM profile_approvals ORDER BY rowid DESC LIMIT 1").get() as
    | { hash: string; approved_at: string }
    | undefined;
  return row ?? null;
}

// Longest-common-subsequence line diff. Profiles are small, so O(n*m) is fine.
export function lineDiff(a: string[], b: string[]): string[] {
  const dp = Array.from({ length: a.length + 1 }, () => new Array<number>(b.length + 1).fill(0));
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) dp[i][j] = a[i] === b[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
  }
  const out: string[] = [];
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    if (a[i] === b[j]) {
      out.push(`  ${a[i]}`);
      i++;
      j++;
    } else if (dp[i + 1][j] >= dp[i][j + 1]) {
      out.push(`- ${a[i++]}`);
    } else {
      out.push(`+ ${b[j++]}`);
    }
  }
  while (i < a.length) out.push(`- ${a[i++]}`);
  while (j < b.length) out.push(`+ ${b[j++]}`);
  return out;
}

function read(file: string): string[] {
  return fs.existsSync(file) ? fs.readFileSync(file, "utf8").split("\n") : [];
}

// Changed lines per file since the last approval (spec §10.5: approve shows the diff first).
export function profileDiff(deps: Deps, db: Ledger, loaded: LoadedProfile): string[] {
  const last = lastApproved(db);
  const oldRoot = last === null ? null : snapshotDir(deps, last.hash);
  const out: string[] = last === null ? ["No approved profile yet; every line is new."] : [];
  const oldFiles = oldRoot !== null && fs.existsSync(oldRoot) ? listRel(oldRoot) : [];
  for (const rel of [...new Set([...oldFiles, ...loaded.files])].sort()) {
    const before = oldRoot === null ? [] : read(path.join(oldRoot, rel));
    // The bytes that were validated and hashed, never a second read (no TOCTOU).
    const after = loaded.bytes[rel] === undefined ? [] : loaded.bytes[rel].toString("utf8").split("\n");
    const changed = lineDiff(before, after).filter((l) => !l.startsWith("  "));
    if (changed.length > 0) out.push(`--- ${rel}`, ...changed);
  }
  return out;
}

function listRel(root: string): string[] {
  const repos = path.join(root, "repos");
  return ["profile.yaml", ...(fs.existsSync(repos) ? fs.readdirSync(repos).map((n) => `repos/${n}`) : [])];
}

// Writes the snapshot from the exact bytes that were validated and hashed, checks
// the snapshot hashes the same, then records the approval. Call inside withEpoch.
export function approveProfile(deps: Deps, db: Ledger, loaded: LoadedProfile): void {
  const dest = snapshotDir(deps, loaded.hash);
  for (const rel of loaded.files) {
    fs.mkdirSync(path.dirname(path.join(dest, rel)), { recursive: true, mode: 0o700 });
    fs.writeFileSync(path.join(dest, rel), loaded.bytes[rel], { mode: 0o600 });
  }
  if (profileHash(dest, loaded.files) !== loaded.hash) {
    throw new SindriError("SND-PROFILE-006", "the approval snapshot doesn't match what was validated", { fix: "sindri profile approve" });
  }
  // Re-approving an earlier profile (a rollback) makes it the latest approval again:
  // delete and re-insert, so its rowid is the newest.
  db.prepare("DELETE FROM profile_approvals WHERE hash = ?").run(loaded.hash);
  db.prepare("INSERT INTO profile_approvals (hash, approved_at, approved_by) VALUES (?, ?, ?)").run(
    loaded.hash, deps.now().toISOString(), deps.system.username(),
  );
}

// The one meaning of "approved" (spec §8.7, plan amendment 8), shared by
// `profile approve`, `doctor` and `observe`. A profile change takes effect only
// once approved: runtime commands load the latest approval's snapshot, never the
// live files, and the live profile is approved only when it is that snapshot.
export type ApprovalState =
  | { kind: "approved"; approved: LoadedProfile }
  | { kind: "never-approved" }
  | { kind: "changed-since-approval"; approved: LoadedProfile }
  | { kind: "snapshot-missing"; hash: string };

export function approvalState(deps: Deps, db: Ledger, liveHash: string): ApprovalState {
  const last = lastApproved(db);
  if (last === null) return { kind: "never-approved" };
  const r = loadProfile(snapshotDir(deps, last.hash));
  if (!r.ok || r.value.hash !== last.hash) return { kind: "snapshot-missing", hash: last.hash };
  return { kind: r.value.hash === liveHash ? "approved" : "changed-since-approval", approved: r.value };
}

// One sentence per state that isn't "approved", naming the next step.
export function approvalProblem(state: Exclude<ApprovalState, { kind: "approved" }>, liveHash: string): string {
  const live = liveHash.slice(0, 12);
  switch (state.kind) {
    case "never-approved":
      return `profile ${live} has never been approved`;
    case "changed-since-approval":
      return `the live profile ${live} differs from the approved profile ${state.approved.hash.slice(0, 12)}, which runs use`;
    case "snapshot-missing":
      return `the snapshot of approved profile ${state.hash.slice(0, 12)} is missing or invalid; re-approve`;
  }
}
