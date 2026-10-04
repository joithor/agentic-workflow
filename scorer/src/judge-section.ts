import type Database from "better-sqlite3";

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

function p50(sorted: number[]): number {
  if (sorted.length === 0) return 0;
  return sorted[Math.ceil(sorted.length * 0.5) - 1] as number;
}

// A question can appear here with zero decided rows (every call so far was
// disabled/invalid-input/never-decided), so `sorted` can be empty.
function p95(sorted: number[]): number {
  if (sorted.length === 0) return 0;
  const idx = Math.min(sorted.length - 1, Math.ceil(sorted.length * 0.95) - 1);
  return sorted[idx] as number;
}

interface DecisionAggRow {
  latency_ms: number;
  undone_at: string | null;
  skipped: string;
  provider: string;
}

export function judgeSection(db: JudgeDb, sinceIso: string): JudgeReportRow[] {
  const questions = db.prepare("SELECT DISTINCT question FROM decisions WHERE ts >= ? ORDER BY question").all(sinceIso) as Array<{ question: string }>;
  // An older judge db has no decision_details (RF-2): agreement counts are 0.
  const hasDetails = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='decision_details'").get() !== undefined;
  return questions.map(({ question }) => {
    // Only decided rows carry a real latency/undo/fallback story — an
    // escalated or failed row never decided anything (decision is NULL).
    const decisionRows = db
      .prepare("SELECT latency_ms, undone_at, skipped, provider FROM decisions WHERE question = ? AND ts >= ? AND outcome = 'decided'")
      .all(question, sinceIso) as DecisionAggRow[];
    const decisions = decisionRows.length;
    const undos = decisionRows.filter((r) => r.undone_at !== null).length;
    // A real fallback: this decision's chain skipped at least one provider
    // before the winner, per the `skipped` array evaluate() records —
    // independent of the failures table, since a provider can be skipped for
    // being merely unavailable (no failure row) as well as for erroring.
    const fallbacks = decisionRows.filter((r) => (JSON.parse(r.skipped) as unknown[]).length > 0).length;
    const failures = (db.prepare("SELECT COUNT(*) as n FROM failures WHERE question = ? AND ts >= ?").get(question, sinceIso) as { n: number }).n;
    const escalations = (
      db.prepare("SELECT COUNT(*) as n FROM decisions WHERE question = ? AND ts >= ? AND outcome IN ('escalated', 'failed')").get(question, sinceIso) as { n: number }
    ).n;
    const latencies = decisionRows.map((r) => r.latency_ms).sort((a, b) => a - b);
    const agreementCounts = { undecided: 0, agreed: 0, overrode: 0 };
    if (hasDetails) {
      const rows = db
        .prepare(
          "SELECT dd.agreement AS agreement, COUNT(*) AS n FROM decisions d JOIN decision_details dd ON dd.id = d.id WHERE d.question = ? AND d.ts >= ? AND d.outcome = 'decided' AND dd.agreement IS NOT NULL GROUP BY dd.agreement",
        )
        .all(question, sinceIso) as Array<{ agreement: keyof typeof agreementCounts; n: number }>;
      for (const r of rows) agreementCounts[r.agreement] = r.n;
    }
    const byProvider: Record<string, number> = {};
    for (const r of decisionRows) byProvider[r.provider] = (byProvider[r.provider] ?? 0) + 1;
    return {
      question,
      decisions,
      undos,
      errorRate: decisions > 0 ? undos / decisions : 0,
      failures,
      fallbacks,
      p95LatencyMs: p95(latencies),
      escalations,
      ...agreementCounts,
      p50LatencyMs: p50(latencies),
      byProvider,
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
