import { percentile } from "./metrics.js";

// One decision as the summary needs it. `agreement` comes from decision_details
// (Plan A) and is null for decisions made before it was recorded.
export interface DecisionFacts {
  provider: string;
  outcome: string;
  latency_ms: number;
  agreement: string | null;
}

export interface DecisionSummary {
  calls: number;
  byProvider: Record<string, number>;
  undecided: number;
  agreed: number;
  overrode: number;
  p50LatencyMs: number;
  p95LatencyMs: number;
}

// The one definition of these figures. The live pane (live-judge.ts) and the daily
// report's judge section (judge-section.ts) both call it, so they cannot disagree.
//   calls         every decision row, whatever its outcome
//   byProvider    decided rows only: who actually answered
//   latency       decided rows only: an escalated or failed row never answered
export function summarizeDecisions(rows: readonly DecisionFacts[]): DecisionSummary {
  const decided = rows.filter((r) => r.outcome === "decided");
  const byProvider: Record<string, number> = {};
  for (const r of decided) byProvider[r.provider] = (byProvider[r.provider] ?? 0) + 1;
  const latencies = decided.map((r) => r.latency_ms);
  // Agreement semantics (matches the daily report): only decided rows count. An escalated or
  // failed row never answered, so it can neither agree with nor override the rules.
  const count = (agreement: string): number => decided.filter((r) => r.agreement === agreement).length;
  return {
    calls: rows.length,
    byProvider,
    undecided: count("undecided"),
    agreed: count("agreed"),
    overrode: count("overrode"),
    p50LatencyMs: percentile(latencies, 0.5),
    p95LatencyMs: percentile(latencies, 0.95),
  };
}
