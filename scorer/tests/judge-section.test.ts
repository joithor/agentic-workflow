import { describe, expect, it } from "vitest";

import { openDb, recordDecision, recordDecisionDetails, recordFailure, recordUndo } from "../../judge/src/db.js";
import { judgeSection, renderJudgeSection } from "../src/judge-section.js";

describe("judgeSection", () => {
  it("aggregates decisions, undos, failures, real fallbacks (from skipped, not failures), and p95 latency per question", () => {
    const db = openDb(":memory:");
    const ts = "2026-09-27T00:00:00.000Z";
    for (const [i, latency] of [10, 20, 30, 40].entries()) {
      recordDecision(db, {
        id: `d${i}`, ts, question: "wake-gate", content_class: "message-meta", provider: "claude-cli",
        decision: "send", confidence: 1, reason_code: "claude-cli", latency_ms: latency, input_digest: "x",
        undone_at: null, chain_position: 0, skipped: [], outcome: "decided",
      });
    }
    recordDecision(db, {
      id: "d4", ts, question: "wake-gate", content_class: "message-meta", provider: "claude-cli",
      decision: "send", confidence: 1, reason_code: "claude-cli", latency_ms: 100, input_digest: "x",
      undone_at: null, chain_position: 1, skipped: [{ provider: "jev", reason: "failed" }], outcome: "decided",
    });
    recordUndo(db, "d0", ts);
    recordFailure(db, { ts, question: "wake-gate", provider: "jev", reason_code: "unparseable-result" });

    const rows = judgeSection(db, "2026-09-26T00:00:00.000Z");
    expect(rows).toEqual([
      { question: "wake-gate", decisions: 5, undos: 1, errorRate: 0.2, failures: 1, fallbacks: 1, p95LatencyMs: 100, escalations: 0, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 30, byProvider: { "claude-cli": 5 } },
    ]);
  });

  it("counts a fallback whenever a decision's skipped array is non-empty, independent of the failures table", () => {
    const db = openDb(":memory:");
    const ts = "2026-09-27T00:00:00.000Z";
    recordDecision(db, {
      id: "d0", ts, question: "wake-gate", content_class: "message-meta", provider: "claude-cli",
      decision: "send", confidence: 1, reason_code: "claude-cli", latency_ms: 50, input_digest: "x",
      undone_at: null, chain_position: 1, skipped: [{ provider: "jev", reason: "unavailable" }], outcome: "decided",
    });
    const rows = judgeSection(db, "2026-09-26T00:00:00.000Z");
    expect(rows).toEqual([
      { question: "wake-gate", decisions: 1, undos: 0, errorRate: 0, failures: 0, fallbacks: 1, p95LatencyMs: 50, escalations: 0, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 50, byProvider: { "claude-cli": 1 } },
    ]);
  });

  it("counts escalated and failed outcomes separately from decided rows, and excludes them from decisions/latency/undo/fallback stats", () => {
    const db = openDb(":memory:");
    const ts = "2026-09-27T00:00:00.000Z";
    recordDecision(db, {
      id: "d0", ts, question: "wake-gate", content_class: "message-meta", provider: "claude-cli",
      decision: "send", confidence: 1, reason_code: "claude-cli", latency_ms: 50, input_digest: "x",
      undone_at: null, chain_position: 0, skipped: [], outcome: "decided",
    });
    recordDecision(db, {
      id: "esc0", ts, question: "wake-gate", content_class: "message-meta", provider: "none",
      decision: null, confidence: 0, reason_code: "question-disabled", latency_ms: 0, input_digest: "x",
      undone_at: null, chain_position: 0, skipped: [], outcome: "escalated",
    });
    recordDecision(db, {
      id: "fail0", ts, question: "wake-gate", content_class: "message-meta", provider: "none",
      decision: null, confidence: 0, reason_code: "no-provider-decided", latency_ms: 0, input_digest: "x",
      undone_at: null, chain_position: 2,
      skipped: [{ provider: "jev", reason: "unavailable" }, { provider: "claude-cli", reason: "timeout" }],
      outcome: "failed",
    });
    const rows = judgeSection(db, "2026-09-26T00:00:00.000Z");
    expect(rows).toEqual([
      { question: "wake-gate", decisions: 1, undos: 0, errorRate: 0, failures: 0, fallbacks: 0, p95LatencyMs: 50, escalations: 2, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 50, byProvider: { "claude-cli": 1 } },
    ]);
  });

  it("lists a question that has only escalated/failed rows (no decided row yet)", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "esc0", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta", provider: "none",
      decision: null, confidence: 0, reason_code: "question-disabled", latency_ms: 0, input_digest: "x",
      undone_at: null, chain_position: 0, skipped: [], outcome: "escalated",
    });
    const rows = judgeSection(db, "2026-09-26T00:00:00.000Z");
    expect(rows).toEqual([
      { question: "wake-gate", decisions: 0, undos: 0, errorRate: 0, failures: 0, fallbacks: 0, p95LatencyMs: 0, escalations: 1, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 0, byProvider: {} },
    ]);
  });

  it("adds undecided/agreed/overrode counts, p50 latency and per-provider share from decision_details", () => {
    const db = openDb(":memory:");
    const ts = "2026-10-03T00:00:00.000Z";
    const row = (id: string, provider: string, latency: number) => ({
      id, ts, question: "wake-gate", content_class: "message-meta", provider, decision: "send", confidence: 0.9,
      reason_code: provider, latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const,
    });
    recordDecision(db, row("a", "jev", 300));
    recordDecision(db, row("b", "jev", 400));
    recordDecision(db, row("c", "rules", 0));
    recordDecisionDetails(db, { id: "a", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "agreed" });
    recordDecisionDetails(db, { id: "b", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "overrode" });
    recordDecisionDetails(db, { id: "c", input_json: "{}", probabilities: null, rules_opinion: null, agreement: "undecided" });
    const [r] = judgeSection(db, "2026-10-01T00:00:00.000Z");
    expect(r).toMatchObject({ undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 300, byProvider: { jev: 2, rules: 1 } });
  });

  it("renders when the judge db predates decision_details (RF-2)", () => {
    const db = openDb(":memory:");
    db.exec("DROP TABLE decision_details");
    recordDecision(db, { id: "a", ts: "2026-10-03T00:00:00.000Z", question: "q", content_class: "brief", provider: "jev", decision: "x", confidence: 1, reason_code: "jev", latency_ms: 1, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    expect(judgeSection(db, "2026-10-01T00:00:00.000Z")[0]).toMatchObject({ undecided: 0, agreed: 0, overrode: 0 });
  });

  it("excludes decisions before the since timestamp", () => {
    const db = openDb(":memory:");
    recordDecision(db, { id: "old", ts: "2020-01-01T00:00:00.000Z", question: "wake-gate", content_class: "message-meta", provider: "rules", decision: "drop", confidence: 1, reason_code: "pre-rule", latency_ms: 1, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    expect(judgeSection(db, "2026-01-01T00:00:00.000Z")).toEqual([]);
  });

  it("returns an empty array, not an error, with no data at all", () => {
    expect(judgeSection(openDb(":memory:"), "2026-01-01T00:00:00.000Z")).toEqual([]);
  });
});

describe("renderJudgeSection", () => {
  it("renders a markdown table with the section leading with Judge", () => {
    const md = renderJudgeSection([{ question: "wake-gate", decisions: 5, undos: 1, errorRate: 0.2, failures: 1, fallbacks: 1, p95LatencyMs: 100, escalations: 0, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 30, byProvider: { jev: 2, rules: 1 } }]);
    expect(md.startsWith("## Judge")).toBe(true);
    expect(md).toContain("wake-gate");
    expect(md).toContain("100");
    expect(md).toContain("| Undecided | Agreed | Overrode | p50 (ms) | p95 latency (ms) | Providers |");
    expect(md).toContain("jev 2 · rules 1");
  });

  it("renders a placeholder line with no rows", () => {
    expect(renderJudgeSection([])).toBe("## Judge\n\nNo judge decisions recorded yet.\n");
  });
});

describe("prompt-sort telemetry", () => {
  const ts = "2026-10-03T00:00:00.000Z";
  function sortRow(db: ReturnType<typeof openDb>, id: string, latency: number, axes: Array<[string, string]>, run: { would: string[]; fired: string[] }) {
    recordDecision(db, { id, ts, question: "prompt-sort", content_class: "brief", provider: "jev", decision: "small", confidence: 0.9, reason_code: "sorted", latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement: "agreed" });
    for (const [axis, status] of axes) {
      db.prepare("INSERT INTO prompt_sort_axes (decision_id, axis, value, heuristic, source, status, probability, confidence) VALUES (?, ?, 'true', 'true', 'judge', ?, NULL, NULL)").run(id, axis, status);
    }
    db.prepare("INSERT INTO prompt_sort_runs (decision_id, mode, reason, would_fire, fired, suppressed) VALUES (?, 'blend', 'sorted', ?, ?, '[]')").run(id, JSON.stringify(run.would), JSON.stringify(run.fired));
  }

  it("adds per-axis agreed/overrode/undecided counts and per-scaffold would-fire/fired counts to the prompt-sort row only", () => {
    const db = openDb(":memory:");
    sortRow(db, "a", 300, [["is_task", "agreed"], ["touches_ui", "overrode"]], { would: ["ui-evidence"], fired: [] });
    sortRow(db, "b", 500, [["is_task", "agreed"], ["touches_ui", "undecided"]], { would: ["ui-evidence", "bugfix"], fired: ["bugfix"] });
    recordDecision(db, { id: "w", ts, question: "wake-gate", content_class: "message-meta", provider: "jev", decision: "send", confidence: 1, reason_code: "jev", latency_ms: 1, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    const rows = judgeSection(db, "2026-10-01T00:00:00.000Z");
    const sort = rows.find((r) => r.question === "prompt-sort");
    expect(sort).toMatchObject({ decisions: 2 });
    expect(sort?.axes).toEqual([
      { axis: "is_task", agreed: 2, overrode: 0, undecided: 0 },
      { axis: "touches_ui", agreed: 0, overrode: 1, undecided: 1 },
    ]);
    expect(sort?.scaffolds).toEqual([{ id: "bugfix", wouldFire: 1, fired: 1 }, { id: "ui-evidence", wouldFire: 2, fired: 0 }]);
    expect(rows.find((r) => r.question === "wake-gate")?.axes).toBeUndefined();
    const md = renderJudgeSection(rows);
    expect(md).toContain("### Prompt sorter");
    expect(md).toContain("| touches_ui | 0 | 1 | 1 |");
    expect(md).toContain("| ui-evidence | 2 | 0 |");
  });

  it("renders without the sorter tables on an old judge db (RF-2 of Plan A)", () => {
    const db = openDb(":memory:");
    sortRow(db, "a", 300, [["is_task", "agreed"]], { would: [], fired: [] });
    db.exec("DROP TABLE prompt_sort_axes; DROP TABLE prompt_sort_runs;");
    const sort = judgeSection(db, "2026-10-01T00:00:00.000Z").find((r) => r.question === "prompt-sort");
    expect(sort?.axes).toEqual([]);
    expect(sort?.scaffolds).toEqual([]);
    expect(renderJudgeSection([sort as NonNullable<typeof sort>])).not.toContain("### Prompt sorter");
  });

  it("still reports the prompt-sort row when decision_details is also missing", () => {
    const db = openDb(":memory:");
    sortRow(db, "a", 300, [["is_task", "agreed"]], { would: [], fired: [] });
    db.exec("DROP TABLE decision_details; DROP TABLE prompt_sort_axes; DROP TABLE prompt_sort_runs;");
    const sort = judgeSection(db, "2026-10-01T00:00:00.000Z").find((r) => r.question === "prompt-sort");
    expect(sort).toMatchObject({ decisions: 1, agreed: 0, axes: [], scaffolds: [] });
  });
});
