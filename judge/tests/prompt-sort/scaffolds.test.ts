// judge/tests/prompt-sort/scaffolds.test.ts
import { describe, expect, it } from "vitest";

import { blend, type JudgeReads } from "../../src/prompt-sort/blend.js";
import { heuristicSort } from "../../src/prompt-sort/heuristics.js";
import { NAMES_WORKFLOW, SCAFFOLDS, buildContext, planScaffolds } from "../../src/prompt-sort/scaffolds.js";

const ALL_ON = { brief: true, bugfix: true, "ui-evidence": true, "plan-first": true };
const ALL_OFF = { brief: false, bugfix: false, "ui-evidence": false, "plan-first": false };
const decisive = (over: JudgeReads = {}): JudgeReads => ({
  is_task: { noul: 0.95 }, wants_loop: { noul: 0.05 }, verification_defined: { noul: 0.05 }, is_bug_report: { noul: 0.9 },
  touches_ui: { noul: 0.9 }, complexity: { level: "small", value: 0.33, confidence: 0.8 }, ambiguity: { level: "vague", value: 0.85, confidence: 0.8 }, ...over,
});
const heur = heuristicSort("Fix the login button crash on the settings page");
const plan = (reads: JudgeReads | null, o: Partial<Parameters<typeof planScaffolds>[0]> = {}) => {
  const b = blend(heur, reads);
  return planScaffolds({ values: b.values, axes: b.axes, switches: ALL_ON, lastFired: {}, promptIndex: 1, cooldownPrompts: 5, namesWorkflow: false, ...o });
};

describe("scaffold table", () => {
  it("never names a generic skill; /bugFixOrchestrator is the only workflow mentioned", () => {
    const text = SCAFFOLDS.map((s) => s.text).join("\n");
    expect(text).toContain("/bugFixOrchestrator");
    expect(text.match(/\/[A-Za-z]+/g)).toEqual(["/bugFixOrchestrator"]);
  });

  it("fires brief, bugfix and ui-evidence for a vague UI bug report with no verification", () => {
    const p = plan(decisive());
    expect(p.wouldFire).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect(p.fire.map((s) => s.id)).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect(p.requirementUi).toBe(true);
  });

  it("fires plan-first only for large work, never brief for trivial work, and only the bug pointer for a non-task prompt", () => {
    expect(plan(decisive({ complexity: { level: "large", value: 1, confidence: 0.9 } })).wouldFire).toContain("plan-first");
    expect(plan(decisive({ complexity: { level: "trivial", value: 0, confidence: 0.9 } })).wouldFire).not.toContain("brief");
    // Not a task: only the bug-report pointer remains (a bug report need not be phrased as a request).
    expect(plan(decisive({ is_task: { noul: 0.05 } })).wouldFire).toEqual(["bugfix"]);
    expect(plan(decisive({ is_task: { noul: 0.05 }, is_bug_report: { noul: 0.05 } })).wouldFire).toEqual([]);
  });

  it("records would-fire but fires nothing while every switch is off (shadow mode)", () => {
    const p = plan(decisive(), { switches: ALL_OFF });
    expect(p.wouldFire).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect(p.fire).toEqual([]);
    expect(p.requirementUi).toBe(false);
    expect(p.suppressed.every((s) => s.reason === "switch-off")).toBe(true);
  });

  it("never fires on a heuristic-only axis: the driving axis must be judge-decided (RF-3)", () => {
    const p = plan(null);
    expect(p.wouldFire).toEqual([]);
    expect(p.fire).toEqual([]);
    // The heuristic alone would have suggested bugfix and ui-evidence (brief needs non-trivial complexity); judge-less, neither may fire.
    expect(p.suppressed).toEqual([{ id: "bugfix", reason: "heuristic-only" }, { id: "ui-evidence", reason: "heuristic-only" }]);
  });

  it("applies a per-scaffold cooldown but keeps the UI requirement (RF-3, RF-4)", () => {
    const p = plan(decisive(), { lastFired: { brief: 3, "ui-evidence": 3 }, promptIndex: 5, cooldownPrompts: 5 });
    expect(p.fire.map((s) => s.id)).toEqual(["bugfix"]);
    expect(p.suppressed).toEqual([{ id: "brief", reason: "cooldown" }, { id: "ui-evidence", reason: "cooldown" }]);
    expect(p.requirementUi).toBe(true);
    expect(plan(decisive(), { lastFired: { brief: 1 }, promptIndex: 6, cooldownPrompts: 5 }).fire.map((s) => s.id)).toContain("brief");
  });

  it("does not interrupt an autonomous-loop request with brief/plan-first", () => {
    const p = plan(decisive({ wants_loop: { noul: 0.9 }, complexity: { level: "large", value: 1, confidence: 0.9 } }));
    expect(p.fire.map((s) => s.id)).toEqual(["bugfix", "ui-evidence"]);
    expect(p.suppressed).toEqual([{ id: "brief", reason: "loop-requested" }, { id: "plan-first", reason: "loop-requested" }]);
  });

  it("never scaffolds a prompt that already names a workflow (C8)", () => {
    const p = plan(decisive({ complexity: { level: "large", value: 1, confidence: 0.9 } }), { namesWorkflow: true });
    expect(p.wouldFire).toEqual(["brief", "bugfix", "ui-evidence", "plan-first"]);
    expect(p.fire).toEqual([]);
    expect(p.requirementUi).toBe(false);
    expect(p.suppressed).toEqual(["brief", "bugfix", "ui-evidence", "plan-first"].map((id) => ({ id, reason: "already-named" })));
  });

  it("brief needs every axis the promotion rule measures judge-decided (C2)", () => {
    expect(SCAFFOLDS.find((s) => s.id === "brief")?.needsJudge).toEqual(["ambiguity", "verification_defined", "is_task"]);
    const p = plan(decisive({ verification_defined: undefined }));
    expect(p.wouldFire).not.toContain("brief");
    expect(p.suppressed).toContainEqual({ id: "brief", reason: "heuristic-only" });
  });

  it("detects prompts that already name a workflow", () => {
    expect(NAMES_WORKFLOW.test("run bugFixOrchestrator on FRN-1")).toBe(true);
    expect(NAMES_WORKFLOW.test("use rootCause first")).toBe(true);
    expect(NAMES_WORKFLOW.test("fix the button")).toBe(false);
  });

  it("builds a short context block, empty when nothing fires", () => {
    expect(buildContext([])).toBe("");
    const ctx = buildContext(SCAFFOLDS.filter((s) => s.id === "brief" || s.id === "ui-evidence"));
    expect(ctx.startsWith("Prompt sorter (automatic, may be wrong; skip any note that does not fit):\n- ")).toBe(true);
    expect(ctx.split("\n")).toHaveLength(3);
    expect(ctx.length).toBeLessThan(700);
  });
});
