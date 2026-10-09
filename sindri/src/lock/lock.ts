import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { ulid } from "../ids.js";
import { bumpEpoch, type Ledger } from "../ledger/db.js";
import type { SystemProbe } from "../system.js";

export interface LockOwner {
  pid: number;
  pidStartTime: string | null;
  host: string;
  bootId: string | null;
  startedAt: string;
  epoch: number;
}

export type LockResult = { ok: true; owner: LockOwner; release: () => void } | { ok: false; heldBy: LockOwner | null; detail: string };

export interface LockOptions {
  dir: string;
  db: Ledger;
  sys: SystemProbe;
  now: () => Date;
}

const LOCK = "sindri.lock";
// An owner file that can't be parsed is treated as live this long, then as dead.
// acquire writes owner.json before the rename, so a readable lock is the norm.
const UNREADABLE_GRACE_MS = 60_000;

const OwnerSchema = z.object({
  pid: z.number().int(),
  pidStartTime: z.string().nullable(),
  host: z.string(),
  bootId: z.string().nullable(),
  startedAt: z.string(),
  epoch: z.number().int(),
});

function readOwner(lockDir: string): LockOwner | null {
  try {
    const parsed = OwnerSchema.safeParse(JSON.parse(fs.readFileSync(path.join(lockDir, "owner.json"), "utf8")));
    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}

function writeOwner(lockDir: string, owner: LockOwner): void {
  const tmp = path.join(lockDir, "owner.json.tmp");
  fs.writeFileSync(tmp, JSON.stringify(owner));
  fs.renameSync(tmp, path.join(lockDir, "owner.json"));
}

// false when the target exists (or the source vanished): the caller re-checks.
export function tryRename(from: string, to: string): boolean {
  try {
    fs.renameSync(from, to);
    return true;
  } catch (e) {
    const code = (e as NodeJS.ErrnoException).code;
    if (code === "EEXIST" || code === "ENOTEMPTY" || code === "ENOENT") return false;
    throw e;
  }
}

function sameOwner(a: LockOwner | null, b: LockOwner | null): boolean {
  if (a === null || b === null) return a === b;
  return a.pid === b.pid && a.host === b.host && a.startedAt === b.startedAt;
}

function ageMs(p: string, now: () => Date): number | null {
  const st = fs.statSync(p, { throwIfNoEntry: false });
  return st === undefined ? null : now().getTime() - st.mtimeMs;
}

// "Provably dead" (spec §9.1). A probe that can't answer (null) never counts as
// evidence: an unknown start time is not a reused pid.
function provablyDead(owner: LockOwner | null, ageOfLock: number, sys: SystemProbe): boolean {
  if (owner === null) return ageOfLock > UNREADABLE_GRACE_MS;
  if (owner.host !== sys.hostname()) return false; // v1: one active host; never steal another host's lock
  const boot = sys.bootId();
  if (owner.bootId !== null && boot !== null && owner.bootId !== boot) return true;
  if (!sys.pidAlive(owner.pid)) return true;
  const probe = sys.pidStartTime(owner.pid);
  return owner.pidStartTime !== null && probe !== null && probe !== owner.pidStartTime;
}

function describe(owner: LockOwner | null): string {
  return owner === null ? "locked by an unreadable owner (taken over after 60 s)" : `locked by ${owner.host}/${owner.pid} since ${owner.startedAt}`;
}

// Exclusive takeover: only the process that creates sindri.lock.takeover (mkdir is
// atomic) may move a dead owner's lock aside, so two takers can never both win.
// The mutex holds its taker's owner.json: it is cleared only when that taker is
// provably dead (an unreadable one after 60 s), never just for its age, and a taker
// removes it only while it is still its own.
type Takeover = { kind: "done" | "busy" | "changed" } | { kind: "stuck"; stale: string };

function takeOver(o: LockOptions, lockPath: string, holder: LockOwner | null, me: LockOwner): Takeover {
  const mutex = path.join(o.dir, `${LOCK}.takeover`);
  try {
    fs.mkdirSync(mutex);
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    const age = ageMs(mutex, o.now);
    if (age !== null && provablyDead(readOwner(mutex), age, o.sys)) fs.rmSync(mutex, { recursive: true, force: true });
    return { kind: "busy" };
  }
  try {
    writeOwner(mutex, me);
    if (!sameOwner(readOwner(lockPath), holder)) return { kind: "changed" };
    const stale = path.join(o.dir, `${LOCK}.stale-${ulid(o.now())}`);
    if (tryRename(lockPath, stale)) {
      // Spec §9.1 (M2): confirm the renamed lock is still the dead owner's. If not,
      // put it back and stop; if it can't go back, leave it where it is and say so.
      if (!sameOwner(readOwner(stale), holder)) return tryRename(stale, lockPath) ? { kind: "changed" } : { kind: "stuck", stale };
      fs.rmSync(stale, { recursive: true, force: true });
    }
    return { kind: "done" };
  } finally {
    if (sameOwner(readOwner(mutex), me)) fs.rmSync(mutex, { recursive: true, force: true });
  }
}

function won(o: LockOptions, lockPath: string, me: LockOwner): LockResult {
  let owner: LockOwner;
  try {
    owner = { ...me, epoch: bumpEpoch(o.db) };
  } catch (e) {
    // Never leave a lock behind that no live process will release.
    const gone = path.join(o.dir, `${LOCK}.released-${ulid(o.now())}`);
    tryRename(lockPath, gone);
    fs.rmSync(gone, { recursive: true, force: true });
    throw e;
  }
  writeOwner(lockPath, owner);
  const release = (): void => {
    if (!sameOwner(readOwner(lockPath), owner)) return;
    const gone = path.join(o.dir, `${LOCK}.released-${ulid(o.now())}`);
    if (!tryRename(lockPath, gone)) return;
    // Moved someone else's lock (we were taken over in between): put it back.
    if (!sameOwner(readOwner(gone), owner)) tryRename(gone, lockPath);
    fs.rmSync(gone, { recursive: true, force: true });
  };
  return { ok: true, owner, release };
}

export function acquireTickLock(o: LockOptions): LockResult {
  fs.mkdirSync(o.dir, { recursive: true, mode: 0o700 });
  const lockPath = path.join(o.dir, LOCK);
  const me: LockOwner = {
    pid: o.sys.pid,
    pidStartTime: o.sys.pidStartTime(o.sys.pid),
    host: o.sys.hostname(),
    bootId: o.sys.bootId(),
    startedAt: o.now().toISOString(),
    epoch: 0,
  };
  const tmp = path.join(o.dir, `lock.tmp-${me.pid}-${ulid(o.now())}`);
  fs.mkdirSync(tmp);
  writeOwner(tmp, me);
  try {
    for (let attempt = 0; attempt < 3; attempt++) {
      // Atomic: rename fails while sindri.lock exists (it is never empty).
      if (tryRename(tmp, lockPath)) return won(o, lockPath, me);
      const holder = readOwner(lockPath);
      const age = ageMs(lockPath, o.now);
      if (age === null) continue; // released in between: try again
      if (!provablyDead(holder, age, o.sys)) return { ok: false, heldBy: holder, detail: describe(holder) };
      const t = takeOver(o, lockPath, holder, me);
      if (t.kind === "busy") return { ok: false, heldBy: holder, detail: "another run is taking over a stale lock" };
      // Spec §9.1 M2: the lock changed hands mid-takeover, so this run is a no-op.
      if (t.kind === "changed") return { ok: false, heldBy: readOwner(lockPath), detail: "the lock changed hands during the takeover; left it to its new owner" };
      if (t.kind === "stuck") {
        return { ok: false, heldBy: readOwner(t.stale), detail: `moved a live lock aside and could not restore it; it is at ${t.stale}` };
      }
    }
    return { ok: false, heldBy: readOwner(lockPath), detail: "lost the takeover race" };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

export function inspectLock(dir: string, sys: SystemProbe, now: () => Date): { state: "free" | "held" | "stale"; owner: LockOwner | null; leftovers: string[] } {
  if (!fs.existsSync(dir)) return { state: "free", owner: null, leftovers: [] };
  const leftovers = fs.readdirSync(dir).filter((n) => n.startsWith(`${LOCK}.stale-`) || n.startsWith("lock.tmp-")).sort();
  const lockPath = path.join(dir, LOCK);
  const owner = readOwner(lockPath);
  const age = ageMs(lockPath, now);
  if (age === null) return { state: "free", owner: null, leftovers };
  return { state: provablyDead(owner, age, sys) ? "stale" : "held", owner, leftovers };
}
