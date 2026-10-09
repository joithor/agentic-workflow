import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { awStateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import { tryRename } from "../lock/lock.js";
import type { SystemProbe } from "../system.js";

// The box-wide heavy-job lock (global CLAUDE.md: one heavy job at a time). Same
// primitive as config/lib/locks.sh: mkdir to acquire, rmdir to release. The
// holder record sits beside the dir (<dir>.holder.json) so the dir stays empty for rmdir.
export interface HeavyHolder {
  kind: string;
  pid: number;
  host: string;
  bootId: string | null;
  startedAt: string;
  reclaimed: string | null;
}

const HolderSchema = z.object({
  kind: z.string(),
  pid: z.number().int(),
  host: z.string(),
  bootId: z.string().nullable().default(null),
  startedAt: z.string(),
  reclaimed: z.string().nullable().default(null),
});

// A reclaim mutex with no readable record is treated as live this long, then as dead.
const UNREADABLE_GRACE_MS = 60_000;
// The last resort: a lock held this long is reclaimed whoever holds it. That covers a lock with
// no holder record (a crashed config/lib/locks.sh holder never releases) and a holder whose pid
// an unrelated live process reused. doctor warns at the same age.
export const MAX_HEAVY_AGE_MS = 6 * 3_600_000;

const ageText = (ms: number): string => (ms >= 3_600_000 ? `${Math.floor(ms / 3_600_000)} h` : `${Math.max(0, Math.floor(ms / 60_000))} min`);

// $AW_HEAVY_JOB_LOCK, default <state root>/locks/heavy-job.lock (the path locks.sh callers use).
export const heavyLockDir = (stateRoot: string, env: NodeJS.ProcessEnv): string =>
  env.AW_HEAVY_JOB_LOCK ?? path.join(stateRoot, "locks", "heavy-job.lock");
const holderFile = (dir: string): string => `${dir}.holder.json`;
const mutexDir = (dir: string): string => `${dir}.reclaim`;
const mutexRecord = (dir: string): string => path.join(mutexDir(dir), "holder.json");

function parseHolder(file: string): HeavyHolder | null {
  try {
    const r = HolderSchema.safeParse(JSON.parse(fs.readFileSync(file, "utf8")));
    return r.success ? r.data : null;
  } catch {
    return null;
  }
}

// The lock dir's holder. A record older than the dir is a leftover beside a lock
// taken without one (config/lib/locks.sh), not its holder: never reclaim on it.
function readHolder(dir: string): HeavyHolder | null {
  const h = parseHolder(holderFile(dir));
  const hs = fs.statSync(holderFile(dir), { throwIfNoEntry: false });
  const ds = fs.statSync(dir, { throwIfNoEntry: false });
  return h !== null && hs !== undefined && ds !== undefined && hs.mtimeMs >= ds.mtimeMs ? h : null;
}

function writeHolder(file: string, h: HeavyHolder): void {
  fs.writeFileSync(file, JSON.stringify(h));
}

const sameHolder = (a: HeavyHolder | null, b: HeavyHolder): boolean =>
  a !== null && a.pid === b.pid && a.host === b.host && a.startedAt === b.startedAt;

// Dead on this host: its pid is gone, or it was recorded on another boot (a reused
// pid looks alive after a reboot). A boot id nobody can read is never evidence.
function dead(h: HeavyHolder, sys: SystemProbe): boolean {
  if (h.host !== sys.hostname()) return false;
  const boot = sys.bootId();
  if (h.bootId !== null && boot !== null && h.bootId !== boot) return true;
  return !sys.pidAlive(h.pid);
}

function tryMkdir(dir: string): boolean {
  try {
    fs.mkdirSync(dir);
    return true;
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    return false;
  }
}

// Exclusive takeover, the tick lock's pattern (lock/lock.ts): only the process that
// creates <dir>.reclaim may move a dead holder's lock aside, it re-checks the holder
// inside the mutex, and it keeps the mutex until its own dir and holder record both
// exist, so two waiters on one dead holder can never both enter. The mutex holds its
// taker's record; it is cleared only when that taker is dead (an unreadable one after
// 60 s), and a taker removes it only while it is still its own. true: the lock is ours.
// `gone`: the holder to replace (null: the lock has no holder record); `due`: re-checked on the
// lock dir inside the mutex (its age, for the last-resort reclaim).
function reclaim(deps: Deps, dir: string, gone: HeavyHolder | null, me: HeavyHolder, due: (st: fs.Stats) => boolean): boolean {
  if (!tryMkdir(mutexDir(dir))) {
    const taker = parseHolder(mutexRecord(dir));
    const st = fs.statSync(mutexDir(dir), { throwIfNoEntry: false });
    const stale = taker === null ? st !== undefined && deps.now().getTime() - st.mtimeMs > UNREADABLE_GRACE_MS : dead(taker, deps.system);
    if (stale) fs.rmSync(mutexDir(dir), { recursive: true, force: true });
    return false;
  }
  try {
    writeHolder(mutexRecord(dir), me);
    const st = fs.statSync(dir, { throwIfNoEntry: false });
    const now = st === undefined ? null : readHolder(dir);
    if (st === undefined || !due(st) || (gone === null ? now !== null : !sameHolder(now, gone))) return false; // released, or it changed hands
    // The dir exists, so nobody else can enter while its dead holder's record goes.
    fs.rmSync(holderFile(dir), { force: true });
    const stale = `${dir}.stale-${ulid(deps.now())}`;
    if (!tryRename(dir, stale)) return false;
    // Moved a dir other than the one checked (it changed hands in between): put it back.
    if (fs.statSync(stale).ino !== st.ino) {
      tryRename(stale, dir);
      return false;
    }
    fs.rmdirSync(stale);
    if (!tryMkdir(dir)) return false; // a waiter that wasn't reclaiming got in first
    try {
      writeHolder(holderFile(dir), me);
    } catch (e) {
      fs.rmdirSync(dir);
      throw e;
    }
    return true;
  } finally {
    if (sameHolder(parseHolder(mutexRecord(dir)), me)) fs.rmSync(mutexDir(dir), { recursive: true, force: true });
  }
}

// Only our own lock: the holder names us, or there is none (its write failed). A
// cleanup error is logged, never thrown over the job's own result.
function release(deps: Deps, dir: string, me: HeavyHolder): void {
  try {
    const h = parseHolder(holderFile(dir));
    if (h !== null && !sameHolder(h, me)) return;
    fs.rmSync(holderFile(dir), { force: true });
    fs.rmdirSync(dir);
  } catch (e) {
    deps.log(`could not release the heavy-job lock ${dir}: ${(e as Error).message}`);
  }
}

export async function withHeavyLock<T>(deps: Deps, kind: string, timeoutMs: number, fn: () => Promise<T>): Promise<T> {
  const dir = heavyLockDir(awStateDir(deps), deps.env);
  fs.mkdirSync(path.dirname(dir), { recursive: true });
  const sys = deps.system;
  let me: HeavyHolder = { kind, pid: sys.pid, host: sys.hostname(), bootId: sys.bootId(), startedAt: deps.now().toISOString(), reclaimed: null };
  let written = false;
  // Bounded by attempts, not wall time, so a fake clock can't loop forever.
  const attempts = Math.ceil(timeoutMs / 1000) + 1;
  for (let i = 1; ; i++) {
    if (tryMkdir(dir)) break;
    const h = readHolder(dir);
    const st = fs.statSync(dir, { throwIfNoEntry: false });
    const aged = (s: fs.Stats): boolean => deps.now().getTime() - s.mtimeMs > MAX_HEAVY_AGE_MS;
    // A holder that is dead on this host (killed, crashed, rebooted) never releases: take the
    // lock over. So is one held past MAX_HEAVY_AGE_MS, whoever holds it.
    const isDead = h !== null && dead(h, sys);
    const why = isDead ? `dead pid ${h.pid} (${h.kind})` : st !== undefined && aged(st) ? `a lock held over 6 h${h === null ? " with no holder record" : ` by ${h.kind} (pid ${h.pid})`}` : null;
    if (why !== null) {
      const taken: HeavyHolder = { ...me, reclaimed: why };
      if (reclaim(deps, dir, h, taken, isDead ? () => true : aged)) {
        deps.log(`reclaiming the heavy-job lock left by ${taken.reclaimed}`);
        me = taken;
        written = true;
        break;
      }
    }
    const cur = readHolder(dir) ?? h;
    const who = cur === null ? "" : ` (${cur.kind}, pid ${cur.pid}, since ${cur.startedAt})`;
    const held = st === undefined ? "" : `; held ${ageText(deps.now().getTime() - st.mtimeMs)}`;
    if (i >= attempts) throw new SindriError("SND-INDEX-001", `the heavy-job lock is busy${who}${held}`);
    if (i === 1) deps.log(`waiting for the heavy-job lock${who}`);
    await deps.sleep(1000);
  }
  try {
    if (!written) writeHolder(holderFile(dir), me);
    return await fn();
  } finally {
    release(deps, dir, me);
  }
}

export function heavyLockState(stateRoot: string, now: () => Date, env: NodeJS.ProcessEnv): { held: boolean; holder: HeavyHolder | null; ageMs: number | null } {
  const dir = heavyLockDir(stateRoot, env);
  const st = fs.statSync(dir, { throwIfNoEntry: false });
  if (st === undefined) return { held: false, holder: null, ageMs: null };
  return { held: true, holder: readHolder(dir), ageMs: now().getTime() - st.mtimeMs };
}
