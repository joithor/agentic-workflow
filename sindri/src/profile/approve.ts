import fs from "node:fs";
import path from "node:path";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import type { Ledger } from "../ledger/db.js";
import { loadProfile, profileHash, type LoadedProfile } from "./load.js";

export function snapshotDir(deps: Deps, hash: string): string {
  return path.join(stateDir(deps), "profile-approved", hash);
}

export function isApproved(db: Ledger, hash: string): boolean {
  return db.prepare("SELECT 1 FROM profile_approvals WHERE hash = ?").get(hash) !== undefined;
}

export function lastApproved(db: Ledger): { hash: string; approved_at: string } | null {
  const row = db.prepare("SELECT hash, approved_at FROM profile_approvals ORDER BY approved_at DESC, rowid DESC LIMIT 1").get() as
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
    const after = read(path.join(loaded.root, rel));
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
  // Re-approving an earlier profile (a rollback) makes it the latest approval again.
  db.prepare(
    "INSERT INTO profile_approvals (hash, approved_at, approved_by) VALUES (?, ?, ?) ON CONFLICT(hash) DO UPDATE SET approved_at = excluded.approved_at, approved_by = excluded.approved_by",
  ).run(
    loaded.hash, deps.now().toISOString(), deps.system.username(),
  );
}

// Spec §8.7: a profile change takes effect only once approved, so runtime
// commands load the last approved snapshot, not the live files.
export function approvedProfile(deps: Deps, db: Ledger): LoadedProfile | null {
  const last = lastApproved(db);
  if (last === null) return null;
  const r = loadProfile(snapshotDir(deps, last.hash));
  return r.ok && r.value.hash === last.hash ? r.value : null;
}
