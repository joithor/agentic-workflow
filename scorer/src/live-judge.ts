import fs from "node:fs";

import Database from "better-sqlite3";
import { z } from "zod";

import type { DecisionFacts } from "./judge-stats.js";
import { summarizeDecisions } from "./judge-stats.js";
import { percentile } from "./metrics.js";

export type JudgeDb = Database.Database;

// The hooks that fire a judge question, by the question they ask.
export const GATES = { "scope-gate": "brief-scope", "done-gate": "ask-check", "send-gate": "wake-gate" } as const;

const Count = z.number().int().nonnegative();
const GateStatsSchema = z.object({
  question: z.string(),
  fired: Count,
  byDecision: z.record(Count),
  p50LatencyMs: z.number().nonnegative(),
});
const LatestSchema = z.object({
  id: z.string(),
  ts: z.string(),
  question: z.string(),
  decision: z.string(),
  provider: z.string(),
  confidence: z.number(),
  itemId: z.string().nullable(),
});

// none      no judge database (judge never ran, or it is unreadable)
// unscoped  a database that predates decision_details.session_id: decisions cannot be tied to a session
// ok        this session's decisions
export const JudgeLiveSchema = z.discriminatedUnion("state", [
  z.object({ state: z.literal("none") }),
  z.object({ state: z.literal("unscoped") }),
  z.object({
    state: z.literal("ok"),
    calls: Count,
    byProvider: z.record(Count),
    undecided: Count,
    agreed: Count,
    overrode: Count,
    p50LatencyMs: z.number().nonnegative(),
    p95LatencyMs: z.number().nonnegative(),
    gates: z.object({ "scope-gate": GateStatsSchema, "done-gate": GateStatsSchema, "send-gate": GateStatsSchema }),
    labelable: z.boolean(),
    latest: LatestSchema.nullable(),
  }),
]);
export type JudgeLive = z.infer<typeof JudgeLiveSchema>;
type GateStats = z.infer<typeof GateStatsSchema>;

interface SessionRow extends DecisionFacts {
  id: string;
  ts: string;
  question: string;
  decision: string | null;
  confidence: number;
}

function hasTable(db: JudgeDb, name: string): boolean {
  return db.prepare("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?").get(name) !== undefined;
}

function hasColumn(db: JudgeDb, table: string, column: string): boolean {
  const cols = db.prepare(`SELECT name FROM pragma_table_info('${table}')`).all() as Array<{ name: string }>;
  return cols.some((c) => c.name === column);
}

function gateStats(rows: readonly SessionRow[], question: string): GateStats {
  const mine = rows.filter((r) => r.question === question);
  const byDecision: Record<string, number> = {};
  for (const r of mine) if (r.decision !== null) byDecision[r.decision] = (byDecision[r.decision] ?? 0) + 1;
  const decided = mine.filter((r) => r.outcome === "decided").map((r) => r.latency_ms);
  return { question, fired: mine.length, byDecision, p50LatencyMs: percentile(decided, 0.5) };
}

// This session's judge activity. `db` may be read-only; nothing here writes.
export function judgeLive(db: JudgeDb, sessionId: string): JudgeLive {
  if (!hasTable(db, "decisions")) return { state: "none" };
  if (!hasTable(db, "decision_details") || !hasColumn(db, "decision_details", "session_id")) return { state: "unscoped" };
  const rows = db.prepare(
    `SELECT d.id, d.ts, d.question, d.provider, d.decision, d.confidence, d.outcome, d.latency_ms, x.agreement
     FROM decisions d JOIN decision_details x ON x.id = d.id
     WHERE x.session_id = ? ORDER BY d.ts ASC, d.id ASC`,
  ).all(sessionId) as SessionRow[];
  const labelable = hasTable(db, "eval_items");
  const last = rows.filter((r) => r.outcome === "decided" && r.decision !== null).at(-1);
  let latest: z.infer<typeof LatestSchema> | null = null;
  if (last !== undefined) {
    const item = labelable
      ? (db.prepare("SELECT id FROM eval_items WHERE source = ?").get(`decision:${last.id}`) as { id: string } | undefined)
      : undefined;
    latest = {
      id: last.id, ts: last.ts, question: last.question, decision: last.decision as string,
      provider: last.provider, confidence: last.confidence, itemId: item?.id ?? null,
    };
  }
  return {
    state: "ok",
    ...summarizeDecisions(rows),
    gates: {
      "scope-gate": gateStats(rows, GATES["scope-gate"]),
      "done-gate": gateStats(rows, GATES["done-gate"]),
      "send-gate": gateStats(rows, GATES["send-gate"]),
    },
    labelable,
    latest,
  };
}

function openReadOnly(dbPath: string): JudgeDb | null {
  let db: JudgeDb | null = null;
  try {
    db = new Database(dbPath, { readonly: true, fileMustExist: true });
  } catch {
    // unreadable: stays null
  }
  return db;
}

// Opens the judge database read-only for one query. Any trouble (missing file,
// corrupt file, a writer holding it) reads as "no judge": the pane must never fail over this.
export function readJudgeLive(dbPath: string, sessionId: string): JudgeLive {
  const db = fs.existsSync(dbPath) ? openReadOnly(dbPath) : null;
  if (db === null) return { state: "none" };
  let live: JudgeLive = { state: "none" };
  try {
    db.pragma("busy_timeout = 200");
    live = judgeLive(db, sessionId);
  } catch {
    // unreadable or malformed: stays "none"
  } finally {
    db.close();
  }
  return live;
}
