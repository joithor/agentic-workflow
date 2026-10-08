import { describe, expect, it } from "vitest";

import type { LabeledTurn } from "../src/audit/calibrate.js";
import { calibrate, estimateWrongApproach, repeatAgreement } from "../src/audit/calibrate.js";
import type { LabelName } from "../src/audit/labels.js";
import { wilson } from "../src/audit/stats.js";

let n = 0;
const mk = (text: string, labels: LabelName[], editsBefore = false): LabeledTurn => ({ key: `s:${(n += 1)}`, text, editsBefore, labels });

describe("calibrate", () => {
  // Synthetic 10-turn fixture. rigor: TP 3 (a, b, d), FP 1 (c), FN 1 (e).
  // image_turn vs defect_report: TP 1 (f), FP 1 (g), FN 1 (h).
  const fixture: LabeledTurn[] = [
    mk("are you sure about this", ["rigor"]),
    mk("double check the output", ["rigor"]),
    mk("please double check the config", ["none"]),
    mk("show me with evidence", ["rigor"]),
    mk("make it faster", ["rigor"]),
    mk("[Image #1] looks off", ["defect_report"]),
    mk("[Image #2] here is the layout", ["none"]),
    mk("the button crashes", ["defect_report"]),
    mk("thanks", ["none"]),
    mk("great work", ["none"]),
  ];

  it("computes TP, FP, FN, precision, recall and Wilson lower bounds per pattern", () => {
    const rows = calibrate(fixture);
    const rigor = rows.find((r) => r.pattern === "rigor");
    expect(rigor).toMatchObject({ label: "rigor", positives: 4, tp: 3, fp: 1, fn: 1, status: "floor only" });
    expect(rigor?.precision.rate).toBe(0.75);
    expect(rigor?.precision.lower).toBeCloseTo(wilson(3, 4).lower, 6);
    expect(rigor?.recall.rate).toBe(0.75);
    expect(rigor?.recall.lower).toBeCloseTo(wilson(3, 4).lower, 6);
    const image = rows.find((r) => r.pattern === "image_turn");
    expect(image).toMatchObject({ label: "defect_report", positives: 2, tp: 1, fp: 1, fn: 1 });
  });

  it("covers exactly the six mapped patterns", () => {
    expect(calibrate(fixture).map((r) => r.pattern).sort()).toEqual(["handoff", "image_turn", "restate", "rigor", "scope_surface", "ship_recipe"]);
  });

  it("marks a pattern metric-grade only with both lower bounds at least 0.6 and at least 10 positives", () => {
    const hit = (i: number): LabeledTurn => mk(`as i mentioned, use the helper ${i}`, ["restate"]);
    const miss = (): LabeledTurn => mk("hello there", ["none"]);
    const grade = (turns: LabeledTurn[]) => calibrate(turns).find((r) => r.pattern === "restate")?.status;
    expect(grade([...Array.from({ length: 12 }, (_, i) => hit(i)), miss()])).toBe("metric-grade");
    expect(grade([...Array.from({ length: 9 }, (_, i) => hit(i)), miss()])).toBe("floor only"); // lower bounds fine, only 9 positives
    const falsePositives = Array.from({ length: 4 }, (_, i) => mk(`as i said, nothing ${i}`, ["none"]));
    expect(grade([...Array.from({ length: 12 }, (_, i) => hit(i)), ...falsePositives])).toBe("floor only"); // precision lower bound about 0.5
    const missed = Array.from({ length: 12 }, () => mk("please redo that", ["restate"]));
    expect(grade([...Array.from({ length: 12 }, (_, i) => hit(i)), ...missed])).toBe("floor only"); // recall lower bound about 0.3
  });

  it("reports zero rates, not NaN, when a pattern never fires and its label never appears", () => {
    const r = calibrate([mk("thanks", ["none"])]).find((x) => x.pattern === "handoff");
    expect(r).toMatchObject({ positives: 0, tp: 0, fp: 0, fn: 0, status: "floor only" });
    expect(r?.precision.rate).toBe(0);
    expect(r?.recall.rate).toBe(0);
  });
});

describe("estimateWrongApproach", () => {
  const sample: LabeledTurn[] = [
    mk("a", ["wrong_approach_design"], true),
    mk("b", ["wrong_approach_design"], false),
    mk("c", ["wrong_approach_process"], true),
    ...Array.from({ length: 7 }, () => mk("d", ["none"], true)),
  ];

  it("scales sample rates to all deduped turns and to 30 days", () => {
    const e = estimateWrongApproach(sample, 1000, 60);
    expect(e).toMatchObject({ sampled: 10, totalTurns: 1000, windowDays: 60 });
    expect(e.design.all).toMatchObject({ inSample: 2, estimated: 200 });
    expect(e.design.all.estimatedLower).toBeCloseTo(wilson(2, 10).lower * 1000, 6);
    expect(e.design.all.estimatedUpper).toBeCloseTo(wilson(2, 10).upper * 1000, 6);
    expect(e.design.afterCode).toMatchObject({ inSample: 1, estimated: 100, per30Days: 50 });
    expect(e.process.all).toMatchObject({ inSample: 1, estimated: 100 });
    expect(e.process.afterCode).toMatchObject({ inSample: 1, estimated: 100, per30Days: 50 });
    expect(e.decision).toBe("deliverable");
  });

  it("applies the decision rule: under 8 design corrections after code per 30 days means not-a-deliverable", () => {
    const e = estimateWrongApproach(sample, 50, 60);
    expect(e.design.afterCode.per30Days).toBeCloseTo(2.5, 6);
    expect(e.decision).toBe("not-a-deliverable");
    expect(e.straddlesThreshold).toBe(true); // the upper bound (about 10) is above 8
  });

  it("reports a zero per-30-day rate when the window has no days", () => {
    expect(estimateWrongApproach(sample, 1000, 0).design.afterCode.per30Days).toBe(0);
  });

  it("reports no-data for an empty sample", () => {
    const e = estimateWrongApproach([], 1000, 60);
    expect(e.decision).toBe("no-data");
    expect(e.design.all.estimated).toBe(0);
  });
});

describe("repeatAgreement", () => {
  it("is the raw per-label agreement of presence between two passes", () => {
    const first = new Map<string, LabelName[]>([["a", ["rigor"]], ["b", ["none"]], ["c", ["rigor", "restate"]], ["d", ["none"]]]);
    const second = new Map<string, LabelName[]>([["a", ["rigor"]], ["b", ["rigor"]], ["c", ["rigor"]], ["d", ["none"]]]);
    const r = repeatAgreement(first, second);
    expect(r.compared).toBe(4);
    expect(r.agreement.rigor).toBe(0.75);
    expect(r.agreement.restate).toBe(0.75);
    expect(r.agreement.none).toBe(0.75);
    expect(r.agreement.handoff).toBe(1);
  });

  it("returns null agreements when nothing was compared", () => {
    const r = repeatAgreement(new Map(), new Map());
    expect(r.compared).toBe(0);
    expect(r.agreement.rigor).toBeNull();
  });
});

describe("calibrate on the labeled text", () => {
  it("does not count a pattern that matches only after character 1500", () => {
    const rows = calibrate([mk(`${"x ".repeat(750)}are you sure`, ["none"]), mk("are you sure", ["rigor"])]);
    expect(rows.find((r) => r.pattern === "rigor")).toMatchObject({ tp: 1, fp: 0, fn: 0 });
  });
});
