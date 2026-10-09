import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { withEpoch, type Ledger } from "../ledger/db.js";
import { acquireTickLock } from "../lock/lock.js";
import type { CommandResult } from "../output.js";
import type { LoadedProfile } from "../profile/load.js";
import type { RepoConfig } from "../profile/schema.js";
import type { ScopeIo } from "../scope/commands.js";

export type EvolveIo = ScopeIo;

export interface EvolveCtx {
  deps: Deps;
  io: EvolveIo;
  loaded: LoadedProfile;
  db: Ledger;
  repo: string; // the ring-0 repo path: the toolkit itself
  prompts: () => readonly { id: string; text: string }[]; // effective prompt texts, registered as prompt artifacts
  write: <T>(fn: (epoch: number) => T) => T; // one attempt
  writeRetry: <T>(fn: (epoch: number) => T) => Promise<T>; // retries a held lock, see withLockedWriteRetry
}

export type Sub = (args: string[], ctx: EvolveCtx) => Promise<CommandResult>;

// Evolve commands do long model and suite work, so they never hold the tick lock across it
// (hourly `observe` must not be blocked). Each ledger write batch takes the lock, fences on the
// epoch it acquired, and releases it (spec §9.1).
export function withLockedWrite<T>(deps: Deps, db: Ledger, fn: (epoch: number) => T): T {
  const lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
  if (!lock.ok) throw new SindriError("SND-LOCK-001", lock.detail);
  try {
    return withEpoch(db, lock.owner.epoch, () => fn(lock.owner.epoch));
  } finally {
    lock.release();
  }
}

export const LOCK_RETRIES = 3;
export const LOCK_WAIT_MS = 2000;

// For writes that follow model work (a lost batch would waste paid output) or that run while the
// hourly `observe` may hold the tick lock. A held lock is retried 3 times, 2 s apart, through
// deps.sleep; any other error, and the fourth refusal, propagate unchanged (SND-LOCK-001).
export async function withLockedWriteRetry<T>(deps: Deps, db: Ledger, fn: (epoch: number) => T): Promise<T> {
  for (let attempt = 0; attempt < LOCK_RETRIES; attempt++) {
    try {
      return withLockedWrite(deps, db, fn);
    } catch (e) {
      if (!(e instanceof SindriError) || e.code !== "SND-LOCK-001") throw e;
      await deps.sleep(LOCK_WAIT_MS);
    }
  }
  return withLockedWrite(deps, db, fn); // the fourth and last attempt: a refusal propagates
}

export const writers = (deps: Deps, db: Ledger): Pick<EvolveCtx, "write" | "writeRetry"> => ({
  write: (fn) => withLockedWrite(deps, db, fn),
  writeRetry: (fn) => withLockedWriteRetry(deps, db, fn),
});

export const repoConfig = (loaded: LoadedProfile): RepoConfig => loaded.repos[loaded.profile.tracker.repo];
export const ringZeroRepo = (loaded: LoadedProfile): string => repoConfig(loaded).path;

export function positiveInt(v: string | undefined, dflt: number, flag: string): number {
  if (v === undefined) return dflt;
  if (!/^[1-9]\d{0,6}$/.test(v)) throw new SindriError("SND-CLI-002", `${flag} must be a positive whole number`);
  return Number(v);
}
