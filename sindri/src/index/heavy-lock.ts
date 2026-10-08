import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { awStateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";

// The box-wide heavy-job lock (global CLAUDE.md: one heavy job at a time). Same
// primitive as config/lib/locks.sh: mkdir to acquire, rmdir to release. The
// holder record sits beside the dir (<dir>.holder.json) so the dir stays empty for rmdir.
export interface HeavyHolder {
  kind: string;
  pid: number;
  host: string;
  startedAt: string;
  reclaimed: string | null;
}

const HolderSchema = z.object({
  kind: z.string(),
  pid: z.number().int(),
  host: z.string(),
  startedAt: z.string(),
  reclaimed: z.string().nullable().default(null),
});

// $AW_HEAVY_JOB_LOCK, default <state root>/locks/heavy-job.lock (the path locks.sh callers use).
export const heavyLockDir = (stateRoot: string, env: NodeJS.ProcessEnv = {}): string =>
  env.AW_HEAVY_JOB_LOCK ?? path.join(stateRoot, "locks", "heavy-job.lock");
const holderFile = (dir: string): string => `${dir}.holder.json`;

function readHolder(dir: string): HeavyHolder | null {
  try {
    const r = HolderSchema.safeParse(JSON.parse(fs.readFileSync(holderFile(dir), "utf8")));
    return r.success ? r.data : null;
  } catch {
    return null;
  }
}

export async function withHeavyLock<T>(deps: Deps, kind: string, timeoutMs: number, fn: () => Promise<T>): Promise<T> {
  const dir = heavyLockDir(awStateDir(deps), deps.env);
  fs.mkdirSync(path.dirname(dir), { recursive: true });
  // Bounded by attempts, not wall time, so a fake clock can't loop forever.
  const attempts = Math.ceil(timeoutMs / 1000) + 1;
  let reclaimed: string | null = null;
  for (let i = 1; ; ) {
    try {
      fs.mkdirSync(dir);
      break;
    } catch (e) {
      if ((e as NodeJS.ErrnoException).code !== "EEXIST") throw e;
    }
    const h = readHolder(dir);
    // A holder that is dead on this host (killed, crashed) never releases: take the lock over.
    if (h !== null && h.host === deps.system.hostname() && !deps.system.pidAlive(h.pid)) {
      reclaimed = `dead pid ${h.pid} (${h.kind})`;
      deps.log(`reclaiming the heavy-job lock left by ${reclaimed}`);
      fs.rmSync(holderFile(dir), { force: true });
      fs.rmSync(dir, { recursive: true, force: true });
      continue;
    }
    const who = h === null ? "" : ` (${h.kind}, pid ${h.pid}, since ${h.startedAt})`;
    if (i >= attempts) throw new SindriError("SND-INDEX-001", `the heavy-job lock is busy${who}`);
    if (i === 1) deps.log(`waiting for the heavy-job lock${who}`);
    await deps.sleep(1000);
    i++;
  }
  try {
    fs.writeFileSync(
      holderFile(dir),
      JSON.stringify({ kind, pid: deps.system.pid, host: deps.system.hostname(), startedAt: deps.now().toISOString(), reclaimed }),
    );
    return await fn();
  } finally {
    fs.rmSync(holderFile(dir), { force: true });
    fs.rmdirSync(dir);
  }
}

export function heavyLockState(
  stateRoot: string,
  now: () => Date,
  env: NodeJS.ProcessEnv = {},
): { held: boolean; holder: HeavyHolder | null; ageMs: number | null } {
  const dir = heavyLockDir(stateRoot, env);
  const st = fs.statSync(dir, { throwIfNoEntry: false });
  if (st === undefined) return { held: false, holder: null, ageMs: null };
  return { held: true, holder: readHolder(dir), ageMs: now().getTime() - st.mtimeMs };
}
