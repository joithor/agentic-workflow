import { describe, expect, it, vi } from "vitest";

import { AXIS_QUESTIONS } from "../../src/prompt-sort/eval-questions.js";
import { makeSortEvalProvider } from "../../src/prompt-sort/eval-provider.js";
import { toRef } from "../../src/question.js";
import { deps, jevBody, jevFetch, noul } from "./fixtures.js";

const PROMPT = "Fix the login button crash on the settings page";
const ref = (axis: string) => {
  const q = AXIS_QUESTIONS[`prompt-sort:${axis}`] as NonNullable<(typeof AXIS_QUESTIONS)[string]>;
  return toRef(q, { prompt: PROMPT });
};

describe("makeSortEvalProvider", () => {
  it("answers each axis from ONE cached sorter run per (mode, prompt)", async () => {
    const fetch = jevFetch(jevBody({ is_bug_report: noul(0.05) }));
    const p = makeSortEvalProvider({ jev: deps(fetch), budgetMs: 1000 });
    expect(await p.decide(ref("is_bug_report"), { prompt: PROMPT }, 1000)).toEqual({ status: "decided", decision: "no", confidence: 1, reason_code: "blend" });
    expect(await p.decide(ref("touches_ui"), { prompt: PROMPT }, 1000)).toMatchObject({ decision: "yes" });
    expect(await p.decide(ref("complexity"), { prompt: PROMPT }, 1000)).toMatchObject({ decision: "small" });
    expect(await p.decide(ref("ambiguity"), { prompt: PROMPT }, 1000)).toMatchObject({ decision: "vague" });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("heuristic mode never calls Jev", async () => {
    const fetch = jevFetch(jevBody());
    const p = makeSortEvalProvider({ jev: deps(fetch), budgetMs: 1000 });
    expect(await p.decide(ref("is_bug_report"), { prompt: PROMPT, mode: "heuristic" }, 1000)).toMatchObject({ decision: "yes", reason_code: "heuristic" });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("judge-only reports an axis the judge left undecided as unavailable", async () => {
    const p = makeSortEvalProvider({ jev: deps(jevFetch(jevBody({ needs_research: noul(0.5) }))), budgetMs: 1000 });
    expect(await p.decide(ref("needs_research"), { prompt: PROMPT, mode: "judge-only" }, 1000)).toEqual({ status: "unavailable", reason_code: "undecided" });
    expect(await p.decide(ref("is_task"), { prompt: PROMPT, mode: "judge-only" }, 1000)).toMatchObject({ status: "decided", decision: "yes" });
  });

  it("C9: during a Jev outage blend and judge-only are unavailable, never heuristics scored as blend", async () => {
    const failing = makeSortEvalProvider({ jev: deps(vi.fn().mockRejectedValue(new Error("x"))), budgetMs: 1000 });
    expect(await failing.decide(ref("is_bug_report"), { prompt: PROMPT }, 1000)).toEqual({ status: "unavailable", reason_code: "jev-unavailable" });
    expect(await failing.decide(ref("is_bug_report"), { prompt: PROMPT, mode: "judge-only" }, 1000)).toEqual({ status: "unavailable", reason_code: "jev-unavailable" });
    expect(await failing.decide(ref("is_bug_report"), { prompt: PROMPT, mode: "heuristic" }, 1000)).toMatchObject({ status: "decided", decision: "yes" });
    const noJev = makeSortEvalProvider({ jev: null, budgetMs: 1000 });
    expect(await noJev.decide(ref("is_task"), { prompt: PROMPT }, 1000)).toEqual({ status: "unavailable", reason_code: "jev-unavailable" });
  });

  it("a judge that answered but decided nothing is still a blend result", async () => {
    const undecided = Object.fromEntries(["is_task", "wants_loop", "scope_defined", "limits_defined", "approach_defined", "verification_defined", "is_bug_report", "touches_ui", "needs_research"].map((a) => [a, noul(0.5)]));
    const p = makeSortEvalProvider({ jev: deps(jevFetch(jevBody({ ...undecided, complexity: { type: "score", score: 1, probabilities: { "1": 1 }, confidence: 0.1 }, ambiguity: { type: "score", score: 1, probabilities: { "1": 1 }, confidence: 0.1 } }))), budgetMs: 1000 });
    expect(await p.decide(ref("is_bug_report"), { prompt: PROMPT }, 1000)).toMatchObject({ status: "decided", reason_code: "blend" });
  });

  it("rejects an unknown question and invalid input", async () => {
    const p = makeSortEvalProvider({ jev: deps(jevFetch(jevBody())), budgetMs: 1000 });
    expect(await p.decide({ name: "wake-gate", outputs: ["send"], prompt: "", contentClass: "brief" }, { prompt: PROMPT }, 1000)).toEqual({ status: "error", reason_code: "not-a-sort-question" });
    expect(await p.decide({ name: "other", outputs: ["send"], prompt: "", contentClass: "brief" }, { prompt: PROMPT }, 1000)).toEqual({ status: "error", reason_code: "not-a-sort-question" });
    expect(await p.decide(ref("is_task"), { nope: 1 }, 1000)).toEqual({ status: "error", reason_code: "invalid-input" });
  });
});
