// judge/tests/prompt-sort/store.test.ts
import crypto from "node:crypto";
import { describe, expect, it } from "vitest";

import { getDecision, getDecisionDetails, openDb } from "../../src/db.js";
import { blend } from "../../src/prompt-sort/blend.js";
import { heuristicSort } from "../../src/prompt-sort/heuristics.js";
import type { SortOutcome } from "../../src/prompt-sort/sort.js";
import { promptSortAxesFor, promptSortRunFor, recordPromptSort } from "../../src/prompt-sort/store.js";

const PROMPT = "Fix the login button crash on the settings page";
const outcome = (reads: Parameters<typeof blend>[1], over: Partial<SortOutcome> = {}): SortOutcome => ({
  ...blend(heuristicSort(PROMPT), reads), mode: reads === null ? "heuristic-only" : "blend", reason: reads === null ? "no-jev" : "sorted",
  failure: null, latencyMs: 380, usage: null, sentPrompt: PROMPT, ...over,
});
const rec = (o: SortOutcome) => ({ id: "d1", ts: "2026-10-04T10:00:00.000Z", sessionId: "s1" as string | null, rawPrompt: PROMPT, outcome: o, wouldFire: [], fired: [], suppressed: [] });

describe("recordPromptSort", () => {
  it("writes ONE decisions row, details with probabilities and agreement, 11 axis rows and the run row", () => {
    const db = openDb(":memory:");
    recordPromptSort(db, { ...rec(outcome({ is_task: { noul: 0.95 }, is_bug_report: { noul: 0.1 }, complexity: { level: "small", value: 0.33, confidence: 0.8 } })), wouldFire: ["bugfix"], fired: [], suppressed: [{ id: "bugfix", reason: "switch-off" }] });
    expect((db.prepare("SELECT COUNT(*) n FROM decisions").get() as { n: number }).n).toBe(1);
    expect(getDecision(db, "d1")).toMatchObject({
      question: "prompt-sort", content_class: "brief", provider: "jev", decision: "small", reason_code: "sorted", latency_ms: 380, outcome: "decided", skipped: [],
      input_digest: crypto.createHash("sha256").update(JSON.stringify({ prompt: PROMPT })).digest("hex").slice(0, 16),
    });
    const details = getDecisionDetails(db, "d1");
    expect(details).toMatchObject({ session_id: "s1", agreement: "overrode", rules_opinion: null });
    expect(JSON.parse(details?.input_json ?? "")).toEqual({ prompt: PROMPT });
    expect(details?.probabilities).toMatchObject({ is_task: 0.95, is_bug_report: 0.1 });
    const axes = promptSortAxesFor(db, "d1");
    expect(axes).toHaveLength(11);
    expect(axes.find((a) => a.axis === "is_bug_report")).toMatchObject({ source: "judge", status: "overrode", value: "false", heuristic: "true" });
    expect(promptSortRunFor(db, "d1")).toEqual({ mode: "blend", reason: "sorted", wouldFire: ["bugfix"], fired: [], suppressed: [{ id: "bugfix", reason: "switch-off" }] });
  });

  it("marks a heuristic-only run as provider rules with agreement 'undecided' and a skipped jev entry", () => {
    const db = openDb(":memory:");
    recordPromptSort(db, rec(outcome(null, { reason: "timeout", failure: "timeout" })));
    expect(getDecision(db, "d1")).toMatchObject({ provider: "rules", reason_code: "heuristic-only:timeout", confidence: 0, skipped: [{ provider: "jev", reason: "timeout" }] });
    expect(getDecisionDetails(db, "d1")?.agreement).toBe("undecided");
    expect(getDecisionDetails(db, "d1")?.probabilities).toBeNull();
  });

  it("distinguishes unavailable from failed, and records no skip when Jev was never asked or answered nothing decisive", () => {
    const db = openDb(":memory:");
    const skipped = (id: string, o: SortOutcome) => {
      recordPromptSort(db, { ...rec(o), id });
      return getDecision(db, id)?.skipped;
    };
    expect(skipped("a", outcome(null, { reason: "no-api-key", failure: null }))).toEqual([{ provider: "jev", reason: "unavailable" }]);
    expect(skipped("b", outcome(null, { reason: "http-503", failure: "http-503" }))).toEqual([{ provider: "jev", reason: "failed" }]);
    expect(skipped("c", outcome(null, { reason: "heuristic-mode" }))).toEqual([]);
    expect(skipped("d", outcome(null, { reason: "no-jev" }))).toEqual([]);
    expect(skipped("e", outcome(null, { reason: "all-undecided" }))).toEqual([]);
  });

  it("records agreement 'agreed' when the judge decided and never disagreed", () => {
    const db = openDb(":memory:");
    recordPromptSort(db, rec(outcome({ is_task: { noul: 0.95 } })));
    expect(getDecisionDetails(db, "d1")?.agreement).toBe("agreed");
  });

  it("stores the redacted, capped prompt in decision_details, never the raw one", () => {
    const db = openDb(":memory:");
    recordPromptSort(db, rec(outcome(null, { sentPrompt: "[redacted] fix it" })));
    expect(getDecisionDetails(db, "d1")?.input_json).toBe(JSON.stringify({ prompt: "[redacted] fix it" }));
  });

  it("promptSortRunFor is undefined for an unknown id", () => {
    expect(promptSortRunFor(openDb(":memory:"), "nope")).toBeUndefined();
  });
});
