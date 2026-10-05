// judge/tests/prompt-sort/promote.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { loadConfig, resolvePromptSort } from "../../src/config.js";
import type { AxisEvalSummary, SortEvalResult } from "../../src/prompt-sort/eval-run.js";
import { SCAFFOLDS } from "../../src/prompt-sort/scaffolds.js";
import { OUTCOME_AXES, RECALL_ONLY_AXES, RULE, SCAFFOLD_AXES, applyPromotion, decidePromotion } from "../../src/prompt-sort/promote.js";

const sum = (axis: AxisEvalSummary["axis"], source: AxisEvalSummary["source"], n: number, h: [number, number | null], b: [number, number | null]): AxisEvalSummary =>
  ({ axis, source, n, heuristic: { accuracy: h[0], ppv: h[1] }, blend: { accuracy: b[0], ppv: b[1] } });
const result = (summaries: AxisEvalSummary[], agreement: SortEvalResult["agreement"] = []): SortEvalResult => ({ ranAt: "2026-10-04T00:00:00.000Z", summaries, agreement });
const verdict = (r: SortEvalResult, id: string) => decidePromotion(r).find((v) => v.scaffold === id);

describe("the fixed rule", () => {
  it("is exactly the numbers written in the plan", () => {
    expect(RULE).toEqual({ minAdjudicator: 60, minOutcome: 40, minLift: 0.1, minAccuracyAdjudicator: 0.75, minAccuracyOutcome: 0.7, minPrecision: 0.7, minAgreement: 0.7, minShared: 20 });
    expect(SCAFFOLD_AXES).toEqual({ brief: ["ambiguity", "verification_defined", "is_task"], bugfix: ["is_bug_report"], "ui-evidence": ["touches_ui"], "plan-first": ["complexity"] });
    expect([...OUTCOME_AXES].sort()).toEqual(["ambiguity", "complexity", "is_bug_report", "is_task", "touches_ui"]);
    expect([...RECALL_ONLY_AXES]).toEqual(["is_bug_report"]);
  });
});

describe("SCAFFOLD_AXES vs runtime needsJudge (C2)", () => {
  it.each(SCAFFOLDS.map((sc) => [sc.id, sc.needsJudge] as const))("%s: needsJudge equals the axes the promotion rule measures", (id, needsJudge) => {
    expect([...needsJudge].sort()).toEqual([...SCAFFOLD_AXES[id]].sort());
  });
});

describe("decidePromotion", () => {
  const good = (axis: AxisEvalSummary["axis"]) => [sum(axis, "adjudicator", 80, [0.6, 0.6], [0.85, 0.8]), sum(axis, "outcome", 50, [0.55, 0.55], [0.8, 0.75])];
  const agree = (axis: AxisEvalSummary["axis"]) => ({ axis, shared: 30, agreed: 24 });

  it("says go when both sources show a >=10 point lift, the floors, precision and agreement", () => {
    const v = verdict(result(good("touches_ui"), [agree("touches_ui")]), "ui-evidence");
    expect(v).toEqual({ scaffold: "ui-evidence", go: true, reasons: [] });
  });

  it.each([
    ["too few adjudicator labels", [sum("touches_ui", "adjudicator", 59, [0.6, 0.6], [0.9, 0.9]), good("touches_ui")[1] as AxisEvalSummary], "touches_ui: adjudicator n=59 < 60"],
    ["too few outcome labels", [good("touches_ui")[0] as AxisEvalSummary, sum("touches_ui", "outcome", 39, [0.5, 0.5], [0.9, 0.9])], "touches_ui: outcome n=39 < 40"],
    ["lift under 10 points on one source", [good("touches_ui")[0] as AxisEvalSummary, sum("touches_ui", "outcome", 50, [0.7, 0.7], [0.79, 0.8])], "touches_ui: outcome lift 0.09 < 0.1"],
    ["accuracy under the floor", [sum("touches_ui", "adjudicator", 80, [0.5, 0.6], [0.74, 0.8]), good("touches_ui")[1] as AxisEvalSummary], "touches_ui: adjudicator accuracy 0.74 < 0.75"],
    ["precision under 0.7", [sum("touches_ui", "adjudicator", 80, [0.6, 0.6], [0.85, 0.69]), good("touches_ui")[1] as AxisEvalSummary], "touches_ui: adjudicator precision 0.69 < 0.7"],
    ["a null precision", [sum("touches_ui", "adjudicator", 80, [0.6, 0.6], [0.85, null]), good("touches_ui")[1] as AxisEvalSummary], "touches_ui: adjudicator precision n/a"],
  ])("says no-go on %s", (_name, summaries, reason) => {
    const v = verdict(result(summaries as AxisEvalSummary[], [agree("touches_ui")]), "ui-evidence");
    expect(v?.go).toBe(false);
    expect(v?.reasons).toContain(reason);
  });

  it("says no-go when the label sources disagree or share too few prompts", () => {
    expect(verdict(result(good("touches_ui"), [{ axis: "touches_ui", shared: 30, agreed: 20 }]), "ui-evidence")?.reasons).toContain("touches_ui: label sources agree 0.67 < 0.7");
    expect(verdict(result(good("touches_ui"), [{ axis: "touches_ui", shared: 19, agreed: 19 }]), "ui-evidence")?.reasons).toContain("touches_ui: only 19 prompts labeled by both sources (< 20)");
    expect(verdict(result(good("touches_ui")), "ui-evidence")?.reasons).toContain("touches_ui: only 0 prompts labeled by both sources (< 20)");
  });

  it("treats is_bug_report outcome labels as recall-only: no precision check on that source", () => {
    const s = [sum("is_bug_report", "adjudicator", 70, [0.6, 0.6], [0.85, 0.8]), sum("is_bug_report", "outcome", 45, [0.5, null], [0.8, null])];
    expect(verdict(result(s, [agree("is_bug_report")]), "bugfix")).toEqual({ scaffold: "bugfix", go: true, reasons: [] });
  });

  it("checks only the adjudicator source for an axis without an outcome labeler (verification_defined)", () => {
    const s = [...good("ambiguity"), ...good("is_task"), sum("verification_defined", "adjudicator", 70, [0.6, 0.6], [0.8, 0.75])];
    const r = result(s, [agree("ambiguity"), agree("is_task")]);
    expect(verdict(r, "brief")).toEqual({ scaffold: "brief", go: true, reasons: [] });
  });

  it("is no-go for every scaffold when nothing was evaluated", () => {
    const all = decidePromotion(result([]));
    expect(all.map((v) => v.scaffold)).toEqual(["brief", "bugfix", "ui-evidence", "plan-first"]);
    expect(all.every((v) => !v.go && v.reasons.length > 0)).toBe(true);
  });
});

describe("applyPromotion", () => {
  it("turns go scaffolds on, never turns anything off, and preserves the rest of the config", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "promote-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: { "wake-gate": { enabled: true, threshold: 0.7 } }, promptSort: { scaffolds: { "plan-first": true } } }));
    const turned = applyPromotion(file, [{ scaffold: "ui-evidence", go: true, reasons: [] }, { scaffold: "bugfix", go: false, reasons: ["x"] }, { scaffold: "plan-first", go: false, reasons: ["x"] }]);
    expect(turned).toEqual(["ui-evidence"]);
    const cfg = loadConfig(file);
    expect(resolvePromptSort(cfg).scaffolds).toEqual({ brief: false, bugfix: false, "ui-evidence": true, "plan-first": true });
    expect(cfg.questions["wake-gate"]).toEqual({ enabled: true, threshold: 0.7 });
  });

  it("creates the config file when none exists", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "promote-")), "judge", "config.json");
    expect(applyPromotion(file, [{ scaffold: "brief", go: true, reasons: [] }])).toEqual(["brief"]);
    expect(resolvePromptSort(loadConfig(file)).scaffolds.brief).toBe(true);
  });
});
