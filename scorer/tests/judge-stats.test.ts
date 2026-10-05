import { describe, expect, it } from "vitest";

import { summarizeDecisions } from "../src/judge-stats.js";

const row = (provider: string, outcome: string, latency: number, agreement: string | null) => ({ provider, outcome, latency_ms: latency, agreement });

describe("summarizeDecisions", () => {
  it("counts every row as a call, but only decided rows as provider answers and latencies", () => {
    expect(summarizeDecisions([
      row("jev", "decided", 300, "agreed"),
      row("jev", "decided", 400, "overrode"),
      row("rules", "decided", 0, "undecided"),
      row("jev", "escalated", 9000, null),
      row("claude-cli", "failed", 9000, null),
    ])).toEqual({
      calls: 5, byProvider: { jev: 2, rules: 1 }, undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 300, p95LatencyMs: 400,
    });
  });

  it("counts agreement only for decided rows, matching the daily report", () => {
    expect(summarizeDecisions([
      row("jev", "decided", 100, "agreed"),
      row("jev", "escalated", 100, "agreed"),
      row("jev", "failed", 100, "overrode"),
      row("rules", "escalated", 100, "undecided"),
    ])).toMatchObject({ calls: 4, agreed: 1, overrode: 0, undecided: 0 });
  });

  it("is all zeros for no rows", () => {
    expect(summarizeDecisions([])).toEqual({ calls: 0, byProvider: {}, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 0, p95LatencyMs: 0 });
  });
});
