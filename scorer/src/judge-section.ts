import type Database from "better-sqlite3";

import type { DecisionFacts } from "./judge-stats.js";
import { summarizeDecisions } from "./judge-stats.js";

export type JudgeDb = Database.Database;

export interface JudgeReportRow {
  question: string;
  decisions: number;
  undos: number;
  errorRate: number;
  failures: number;
  fallbacks: number;
  p95LatencyMs: number;
  escalations: number;
  undecided: number;
  agreed: number;
  overrode: number;
  p50LatencyMs: number;
  byProvider: Record<string, number>;
}

interface DecisionAggRow extends DecisionFacts {
  undone_at: string | null;
  skipped: string;
}

export function judgeSection(db: JudgeDb, sinceIso: string): JudgeReportRow[] {
  const questions = db.prepare("SELECT DISTINCT question FROM decisions WHERE ts >= ? ORDER BY question").all(sinceIso) as Array<{ question: string }>;
  // An older judge db has no decision_details (RF-2): agreement counts are 0.
  const hasDetails = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='decision_details'").get() !== undefined;
  return questions.map(({ question }) => {
    // Every row in the window, whatever its outcome; summarizeDecisions (shared with the live
    // pane) decides which rows count toward each figure. decision_details is left-joined so
    // rows without it carry agreement null.
    const rows = db
      .prepare(
        hasDetails
          ? "SELECT d.provider, d.outcome, d.latency_ms, d.undone_at, d.skipped, dd.agreement FROM decisions d LEFT JOIN decision_details dd ON dd.id = d.id WHERE d.question = ? AND d.ts >= ?"
          : "SELECT provider, outcome, latency_ms, undone_at, skipped, NULL AS agreement FROM decisions WHERE question = ? AND ts >= ?",
      )
      .all(question, sinceIso) as DecisionAggRow[];
    const summary = summarizeDecisions(rows);
    // Only decided rows carry a real undo/fallback story: an escalated or failed row never
    // decided anything (decision is NULL).
    const decidedRows = rows.filter((r) => r.outcome === "decided");
    const decisions = decidedRows.length;
    const undos = decidedRows.filter((r) => r.undone_at !== null).length;
    // A real fallback: this decision's chain skipped at least one provider
    // before the winner, per the `skipped` array evaluate() records —
    // independent of the failures table, since a provider can be skipped for
    // being merely unavailable (no failure row) as well as for erroring.
    const fallbacks = decidedRows.filter((r) => (JSON.parse(r.skipped) as unknown[]).length > 0).length;
    const failures = (db.prepare("SELECT COUNT(*) as n FROM failures WHERE question = ? AND ts >= ?").get(question, sinceIso) as { n: number }).n;
    const escalations = rows.filter((r) => r.outcome === "escalated" || r.outcome === "failed").length;
    return {
      question,
      decisions,
      undos,
      errorRate: decisions > 0 ? undos / decisions : 0,
      failures,
      fallbacks,
      p95LatencyMs: summary.p95LatencyMs,
      escalations,
      undecided: summary.undecided,
      agreed: summary.agreed,
      overrode: summary.overrode,
      p50LatencyMs: summary.p50LatencyMs,
      byProvider: summary.byProvider,
    };
  });
}

function providers(r: JudgeReportRow): string {
  return Object.entries(r.byProvider).map(([name, n]) => `${name} ${n}`).join(" · ");
}

export function renderJudgeSection(rows: JudgeReportRow[]): string {
  if (rows.length === 0) return "## Judge\n\nNo judge decisions recorded yet.\n";
  const header = "## Judge\n\n| Question | Decisions | Undos | Error rate | Failures | Escalations | Fallbacks | Undecided | Agreed | Overrode | p50 (ms) | p95 latency (ms) | Providers |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n";
  const body = rows
    .map((r) => `| ${r.question} | ${r.decisions} | ${r.undos} | ${(r.errorRate * 100).toFixed(1)}% | ${r.failures} | ${r.escalations} | ${r.fallbacks} | ${r.undecided} | ${r.agreed} | ${r.overrode} | ${r.p50LatencyMs} | ${r.p95LatencyMs} | ${providers(r)} |`)
    .join("\n");
  return `${header}${body}\n`;
}
