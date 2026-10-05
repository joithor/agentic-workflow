// judge/src/prompt-sort/store.ts
// ONE decisions row per prompt (question "prompt-sort"), so Plan A's telemetry
// and eval machinery sees it like any other decision. The per-axis answers go
// into two ADDITIVE tables; the decisions schema is untouched. decisions.decision
// holds the complexity level (the headline axis); decisions.confidence is the
// fraction of the 11 axes the judge decided.
import crypto from "node:crypto";

import {
  recordDecision, recordDecisionDetails, type Agreement, type Db, type SkippedProvider,
} from "../db.js";
import { INPUT_CAP, capJson, redactSecrets } from "../redact.js";
import { ALL_AXES } from "./axes.js";
import type { ScaffoldId, SuppressReason } from "./scaffolds.js";
import type { SortOutcome } from "./sort.js";

export interface PromptSortRecord {
  id: string; ts: string; sessionId: string | null; rawPrompt: string; outcome: SortOutcome;
  wouldFire: ScaffoldId[]; fired: ScaffoldId[]; suppressed: Array<{ id: ScaffoldId; reason: SuppressReason }>;
}

function skippedFor(o: SortOutcome): SkippedProvider[] {
  if (o.mode === "blend" || o.reason === "heuristic-mode" || o.reason === "no-jev" || o.reason === "all-undecided") return [];
  return [{ provider: "jev", reason: o.failure === null ? "unavailable" : o.failure === "timeout" ? "timeout" : "failed" }];
}

export function recordPromptSort(db: Db, r: PromptSortRecord): void {
  const o = r.outcome;
  const overrode = o.axes.some((a) => a.status === "overrode");
  const agreement: Agreement = o.judgeDecided === 0 ? "undecided" : overrode ? "overrode" : "agreed";
  const probabilities: Record<string, number> = {};
  for (const a of o.axes) if (a.probability !== null) probabilities[a.axis] = a.probability;

  const write = db.transaction(() => {
    recordDecision(db, {
      id: r.id, ts: r.ts, question: "prompt-sort", content_class: "brief",
      provider: o.judgeDecided > 0 ? "jev" : "rules", decision: o.values.complexity,
      confidence: o.judgeDecided / ALL_AXES.length,
      reason_code: o.mode === "blend" ? "sorted" : `heuristic-only:${o.reason}`, latency_ms: o.latencyMs,
      input_digest: crypto.createHash("sha256").update(JSON.stringify({ prompt: r.rawPrompt })).digest("hex").slice(0, 16),
      undone_at: null, chain_position: 0, skipped: skippedFor(o), outcome: "decided",
    });
    recordDecisionDetails(db, {
      id: r.id, input_json: redactSecrets(capJson({ prompt: o.sentPrompt }, INPUT_CAP)),
      probabilities: Object.keys(probabilities).length === 0 ? null : probabilities,
      rules_opinion: null, agreement, session_id: r.sessionId,
    });
    const axisStmt = db.prepare(
      `INSERT OR REPLACE INTO prompt_sort_axes (decision_id, axis, value, heuristic, source, status, probability, confidence)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
    );
    for (const a of o.axes) {
      axisStmt.run(r.id, a.axis, String(a.value), String(a.heuristic), a.source, a.status, a.probability, a.confidence);
    }
    db.prepare(
      `INSERT OR REPLACE INTO prompt_sort_runs (decision_id, mode, reason, would_fire, fired, suppressed) VALUES (?, ?, ?, ?, ?, ?)`,
    ).run(r.id, o.mode, o.reason, JSON.stringify(r.wouldFire), JSON.stringify(r.fired), JSON.stringify(r.suppressed));
  });
  write();
}

export interface AxisRow {
  axis: string; value: string; heuristic: string; source: string; status: string; probability: number | null; confidence: number | null;
}

export function promptSortAxesFor(db: Db, id: string): AxisRow[] {
  return db
    .prepare("SELECT axis, value, heuristic, source, status, probability, confidence FROM prompt_sort_axes WHERE decision_id = ? ORDER BY axis")
    .all(id) as AxisRow[];
}

export function promptSortRunFor(
  db: Db, id: string,
): { mode: string; reason: string; wouldFire: ScaffoldId[]; fired: ScaffoldId[]; suppressed: Array<{ id: ScaffoldId; reason: SuppressReason }> } | undefined {
  const row = db.prepare("SELECT mode, reason, would_fire, fired, suppressed FROM prompt_sort_runs WHERE decision_id = ?").get(id) as
    { mode: string; reason: string; would_fire: string; fired: string; suppressed: string } | undefined;
  if (row === undefined) return undefined;
  return { mode: row.mode, reason: row.reason, wouldFire: JSON.parse(row.would_fire) as ScaffoldId[], fired: JSON.parse(row.fired) as ScaffoldId[], suppressed: JSON.parse(row.suppressed) as Array<{ id: ScaffoldId; reason: SuppressReason }> };
}
