// judge/tests/prompt-sort/blend.test.ts
import { describe, expect, it } from "vitest";

import { blend, readJudge, scoreRead, type JudgeReads } from "../../src/prompt-sort/blend.js";
import { heuristicSort } from "../../src/prompt-sort/heuristics.js";
import { AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS, COMPLEXITY_CRITERIA } from "../../src/prompt-sort/axes.js";
import { noul, score } from "./fixtures.js";

const ok = (answer: unknown) => ({ ok: true as const, answer: answer as never });
const bad = { ok: false as const, reason_code: "unparseable-result" as const };

describe("scoreRead", () => {
  it("takes the argmax level from probabilities keyed by index", () => {
    const r = scoreRead(score(0, { "0": 0.1, "1": 0.1, "2": 0.1, "3": 0.7 }, 0.9) as never, COMPLEXITY_CRITERIA);
    expect(r).toMatchObject({ index: 3, confidence: 0.9 });
    expect(r.value).toBe(1);
  });

  it("accepts probabilities keyed by short level name or by the full criterion string", () => {
    expect(scoreRead(score(0, { trivial: 0.1, small: 0.1, substantial: 0.1, large: 0.7 }, 0.9) as never, COMPLEXITY_CRITERIA).index).toBe(3);
    const full = Object.fromEntries(COMPLEXITY_CRITERIA.map((c, i) => [c, i === 1 ? 0.9 : 0.03]));
    expect(scoreRead(score(0, full, 0.9) as never, COMPLEXITY_CRITERIA).index).toBe(1);
  });

  it("falls back to the rounded score position when there are no probabilities, clamped", () => {
    expect(scoreRead(score(1.4, {}, 0.7) as never, COMPLEXITY_CRITERIA).index).toBe(1);
    expect(scoreRead(score(9, {}, 0.7) as never, COMPLEXITY_CRITERIA).index).toBe(3);
    expect(scoreRead(score(-2, {}, 0.7) as never, COMPLEXITY_CRITERIA).index).toBe(0);
  });

  it("weights ambiguity so that 'partly' alone stays under 0.5 and real 'vague' mass crosses it", () => {
    expect(scoreRead(score(1, { "0": 0, "1": 1, "2": 0 }, 0.9) as never, AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS).value).toBeCloseTo(0.35);
    expect(scoreRead(score(2, { "0": 0, "1": 0.4, "2": 0.6 }, 0.9) as never, AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS).value).toBeCloseTo(0.74);
    expect(scoreRead(score(2, {}, 0.9) as never, AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS).value).toBe(1);
  });
});

describe("readJudge", () => {
  it("reads noul and score answers and skips malformed ones (RF-2: partial answers)", () => {
    const reads = readJudge({
      is_task: ok(noul(0.9)),
      touches_ui: bad,
      complexity: ok(score(0, { "0": 0.9, "1": 0.05, "2": 0.03, "3": 0.02 }, 0.9)),
      ambiguity: ok(score(1, { "0": 0, "1": 1, "2": 0 }, 0.9)),
    });
    expect(reads.is_task).toEqual({ noul: 0.9 });
    expect(reads.touches_ui).toBeUndefined();
    expect(reads.complexity).toMatchObject({ level: "trivial", confidence: 0.9 });
    expect(reads.ambiguity).toMatchObject({ level: "partly" });
  });

  it("ignores an answer of the wrong type for its axis", () => {
    expect(readJudge({ is_task: ok(score(0, {}, 1)), complexity: ok(noul(0.5)) })).toEqual({});
  });
});

describe("blend", () => {
  const heur = heuristicSort("Fix the login button crash on the settings page"); // task, bug, ui, vague

  it("uses a noul only outside the 0.4-0.6 band", () => {
    const reads: JudgeReads = { is_task: { noul: 0.5 }, wants_loop: { noul: 0.61 }, scope_defined: { noul: 0.35 }, needs_research: { noul: 0.4 } };
    const r = blend(heur, reads);
    const byAxis = Object.fromEntries(r.axes.map((a) => [a.axis, a]));
    expect(byAxis.is_task).toMatchObject({ source: "heuristic", status: "undecided", value: true, probability: 0.5 });
    expect(byAxis.wants_loop).toMatchObject({ source: "judge", value: true, status: "overrode" });
    expect(byAxis.scope_defined).toMatchObject({ source: "judge", value: false, status: "agreed" });
    expect(byAxis.needs_research).toMatchObject({ source: "judge", value: false });
    expect(r.judgeDecided).toBe(3);
  });

  it("uses a score only at confidence >= 0.4", () => {
    const low = blend(heur, { complexity: { level: "large", value: 1, confidence: 0.39 }, ambiguity: { level: "clear", value: 0, confidence: 0.4 } });
    expect(low.values.complexity).toBe(heur.complexity);
    expect(low.values.ambiguity).toBe("clear");
    expect(low.axes.find((a) => a.axis === "ambiguity")).toMatchObject({ source: "judge", status: "overrode", heuristic: "vague", probability: 0 });
    expect(low.axes.find((a) => a.axis === "complexity")).toMatchObject({ source: "heuristic", status: "undecided", confidence: 0.39 });
  });

  it("compares by collapsed class: trivial vs small is agreement, small vs large is not", () => {
    const same = blend(heur, { complexity: { level: "small", value: 0.33, confidence: 0.9 } });
    expect(same.axes.find((a) => a.axis === "complexity")?.status).toBe("agreed");
    const diff = blend(heur, { complexity: { level: "large", value: 1, confidence: 0.9 } });
    expect(diff.axes.find((a) => a.axis === "complexity")?.status).toBe("overrode");
  });

  it("treats a read lacking the axis's field as undecided", () => {
    const r = blend(heur, { is_task: {}, complexity: {}, ambiguity: { confidence: 0.9 } });
    expect(r.judgeDecided).toBe(0);
  });

  it("is pure heuristics, all undecided, when there is no judge at all", () => {
    const r = blend(heur, null);
    expect(r.values).toEqual(heur);
    expect(r.judgeDecided).toBe(0);
    expect(r.axes).toHaveLength(11);
    expect(r.axes.every((a) => a.source === "heuristic" && a.status === "undecided" && a.probability === null)).toBe(true);
  });
});
