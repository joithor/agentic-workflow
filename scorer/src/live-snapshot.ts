import { z } from "zod";

import type { Db } from "./db.js";
import { JudgeLiveSchema } from "./live-judge.js";
import { CTX, OVER_200K } from "./metrics.js";

const Count = z.number().int().nonnegative();

// The contract with the aw-live mod (mods/aw-live/hooks/lib/snapshot.ts). Additive
// changes only; a breaking one bumps `v` and the mod ignores snapshots it does not know.
export const LiveSnapshotSchema = z.object({
  v: z.literal(1),
  sessionId: z.string(),
  at: z.string(),
  transcript: z.object({ found: z.boolean(), files: Count }),
  ingest: z.object({ newLines: Count, ms: z.number().nonnegative(), readErrors: Count }),
  context: z.object({
    tokens: Count.nullable(),
    window: z.number().int().positive(),
    percent: z.number().min(0).max(100).nullable(),
  }),
  usage: z.object({ calls: Count, subagentCalls: Count, contextTokens: Count, outputTokens: Count, callsOver200k: Count }),
  judge: JudgeLiveSchema,
  contextGuard: z.object({ fires: Count }),
  wakes: z.object({ queuedNow: Count }),
});
export type LiveSnapshot = z.infer<typeof LiveSnapshotSchema>;

// Token figures for the session whose files `db` holds, from the same call table, the same
// context-size expression (CTX) and the same 200k line (OVER_200K) the daily report uses.
// Dollar cost is not here: the scorer reads no prices, and the host reports its own.
export function sessionUsage(db: Db, window: number): Pick<LiveSnapshot, "context" | "usage"> {
  const totals = db.prepare(`
    SELECT COUNT(*) AS calls,
      COALESCE(SUM(CASE WHEN is_main = 0 THEN 1 ELSE 0 END), 0) AS sub,
      COALESCE(SUM(${CTX}), 0) AS ctx,
      COALESCE(SUM(output), 0) AS out,
      COALESCE(SUM(CASE WHEN ${CTX} > ${OVER_200K} THEN 1 ELSE 0 END), 0) AS over
    FROM calls`).get() as { calls: number; sub: number; ctx: number; out: number; over: number };
  const last = db.prepare(`SELECT ${CTX} AS tokens FROM calls WHERE is_main = 1 ORDER BY ts DESC, rowid DESC LIMIT 1`).get() as { tokens: number } | undefined;
  const tokens = last?.tokens ?? null;
  return {
    context: { tokens, window, percent: tokens === null ? null : Math.min(100, Math.round((tokens / window) * 100)) },
    usage: { calls: totals.calls, subagentCalls: totals.sub, contextTokens: totals.ctx, outputTokens: totals.out, callsOver200k: totals.over },
  };
}
