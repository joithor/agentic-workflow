import type Database from "better-sqlite3";

import type { DecisionFacts } from "./judge-stats.js";
import { summarizeDecisions } from "./judge-stats.js";

export type JudgeDb = Database.Database;

export interface AxisReportRow {
  axis: string;
  agreed: number;
  overrode: number;
  undecided: number;
}

export interface ScaffoldReportRow {
  id: string;
  wouldFire: number;
  fired: number;
}

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
  axes?: AxisReportRow[];
  scaffolds?: ScaffoldReportRow[];
}

interface DecisionAggRow extends DecisionFacts {
  undone_at: string | null;
  skipped: string;
}

function hasTable(db: JudgeDb, name: string): boolean {
  return db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name) !== undefined;
}

function axisRows(db: JudgeDb, sinceIso: string): AxisReportRow[] {
  if (!hasTable(db, "prompt_sort_axes")) return [];
  return db
    .prepare(
      `SELECT a.axis AS axis,
              SUM(CASE WHEN a.status = 'agreed' THEN 1 ELSE 0 END) AS agreed,
              SUM(CASE WHEN a.status = 'overrode' THEN 1 ELSE 0 END) AS overrode,
              SUM(CASE WHEN a.status = 'undecided' THEN 1 ELSE 0 END) AS undecided
       FROM prompt_sort_axes a JOIN decisions d ON d.id = a.decision_id
       WHERE d.ts >= ? GROUP BY a.axis ORDER BY a.axis`,
    )
    .all(sinceIso) as AxisReportRow[];
}

function scaffoldRows(db: JudgeDb, sinceIso: string): ScaffoldReportRow[] {
  if (!hasTable(db, "prompt_sort_runs")) return [];
  const rows = db
    .prepare("SELECT r.would_fire AS would, r.fired AS fired FROM prompt_sort_runs r JOIN decisions d ON d.id = r.decision_id WHERE d.ts >= ?")
    .all(sinceIso) as Array<{ would: string; fired: string }>;
  const counts = new Map<string, ScaffoldReportRow>();
  const bump = (id: string, key: "wouldFire" | "fired"): void => {
    const row = counts.get(id) ?? { id, wouldFire: 0, fired: 0 };
    row[key]++;
    counts.set(id, row);
  };
  for (const r of rows) {
    let would: string[];
    let fired: string[];
    try {
      would = JSON.parse(r.would) as string[];
      fired = JSON.parse(r.fired) as string[];
    } catch {
      continue; // a corrupt row must not take the whole report down
    }
    for (const id of would) bump(id, "wouldFire");
    for (const id of fired) bump(id, "fired");
  }
  return [...counts.values()].sort((a, b) => a.id.localeCompare(b.id));
}

export function judgeSection(db: JudgeDb, sinceIso: string): JudgeReportRow[] {
  const questions = db.prepare("SELECT DISTINCT question FROM decisions WHERE ts >= ? ORDER BY question").all(sinceIso) as Array<{ question: string }>;
  // An older judge db has no decision_details (RF-2): agreement counts are 0.
  const hasDetails = hasTable(db, "decision_details");
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
      ...(question === "prompt-sort" ? { axes: axisRows(db, sinceIso), scaffolds: scaffoldRows(db, sinceIso) } : {}),
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
  const sorter = rows.find((r) => r.axes !== undefined && r.axes.length > 0);
  const sub = sorter === undefined ? "" : [
    "",
    "### Prompt sorter",
    "",
    "| Axis | Judge agreed with heuristic | Judge overrode heuristic | Undecided (heuristic used) |",
    "|---|---|---|---|",
    ...(sorter.axes as AxisReportRow[]).map((a) => `| ${a.axis} | ${a.agreed} | ${a.overrode} | ${a.undecided} |`),
    "",
    "| Scaffold | Would fire | Fired |",
    "|---|---|---|",
    ...(sorter.scaffolds as ScaffoldReportRow[]).map((s) => `| ${s.id} | ${s.wouldFire} | ${s.fired} |`),
    "",
  ].join("\n");
  return `${header}${body}\n${sub}`;
}
