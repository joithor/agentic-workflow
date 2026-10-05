import { describe, expect, it } from "vitest";

import { openDb, recordDecision, recordDecisionDetails } from "../../judge/src/db.js";
import type { DecisionRow } from "../../judge/src/db.js";
import { judgeSection } from "../src/judge-section.js";
import { summarizeDecisions } from "../src/judge-stats.js";
import type { DecisionFacts } from "../src/judge-stats.js";
import { judgeLive } from "../src/live-judge.js";

type Agreement = "agreed" | "overrode" | "undecided";

function put(db: ReturnType<typeof openDb>, id: string, provider: string, latency: number, outcome: DecisionRow["outcome"], agreement: Agreement | null): void {
  recordDecision(db, {
    id, ts: "2026-10-03T10:00:00.000Z", question: "ask-check", content_class: "brief", provider, decision: outcome === "decided" ? "continue" : null, confidence: 0.9,
    reason_code: provider, latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome,
  });
  if (agreement !== null) recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement, session_id: "s1" });
}

// One question, one session: the daily report and the live pane must compute the same figures
// from the same decisions, because both call summarizeDecisions.
describe("report and live agree", () => {
  it("gives the same provider share, agreement counts and latency percentiles", () => {
    const db = openDb(":memory:");
    put(db, "a", "jev", 300, "decided", "agreed");
    put(db, "b", "jev", 400, "decided", "overrode");
    put(db, "c", "rules", 0, "decided", "undecided");
    put(db, "d", "jev", 380, "decided", "agreed");
    put(db, "e", "claude-cli", 6200, "decided", "agreed");
    const [report] = judgeSection(db, "2026-10-01T00:00:00.000Z");
    const live = judgeLive(db, "s1");
    if (live.state !== "ok") throw new Error("expected an ok judge");
    expect(report).toMatchObject({
      byProvider: live.byProvider, agreed: live.agreed, overrode: live.overrode, undecided: live.undecided,
      p50LatencyMs: live.p50LatencyMs, p95LatencyMs: live.p95LatencyMs,
    });
  });

  it("matches summarizeDecisions on a mix of decided and non-decided rows, with and without decision_details", () => {
    const db = openDb(":memory:");
    put(db, "a", "jev", 300, "decided", "agreed");
    put(db, "b", "jev", 400, "decided", "overrode");
    put(db, "c", "rules", 5, "decided", "undecided");
    put(db, "d", "claude-cli", 6200, "decided", null);
    put(db, "e", "jev", 90, "escalated", "agreed");
    put(db, "f", "claude-cli", 9000, "failed", "overrode");
    put(db, "g", "rules", 1, "escalated", null);
    const facts: DecisionFacts[] = [
      { provider: "jev", outcome: "decided", latency_ms: 300, agreement: "agreed" },
      { provider: "jev", outcome: "decided", latency_ms: 400, agreement: "overrode" },
      { provider: "rules", outcome: "decided", latency_ms: 5, agreement: "undecided" },
      { provider: "claude-cli", outcome: "decided", latency_ms: 6200, agreement: null },
      { provider: "jev", outcome: "escalated", latency_ms: 90, agreement: "agreed" },
      { provider: "claude-cli", outcome: "failed", latency_ms: 9000, agreement: "overrode" },
      { provider: "rules", outcome: "escalated", latency_ms: 1, agreement: null },
    ];
    const expected = summarizeDecisions(facts);
    const [report] = judgeSection(db, "2026-10-01T00:00:00.000Z");
    expect(report).toMatchObject({
      byProvider: expected.byProvider, agreed: expected.agreed, overrode: expected.overrode, undecided: expected.undecided,
      p50LatencyMs: expected.p50LatencyMs, p95LatencyMs: expected.p95LatencyMs, decisions: 4, escalations: 3,
    });
    expect(expected).toMatchObject({ agreed: 1, overrode: 1, undecided: 1, byProvider: { jev: 2, rules: 1, "claude-cli": 1 } });
  });
});
