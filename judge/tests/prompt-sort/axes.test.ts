// judge/tests/prompt-sort/axes.test.ts
import { describe, expect, it } from "vitest";

import {
  ALL_AXES, AXIS_COLLAPSE, JEV_QUESTIONS, axisClass, axisLabel, axisOutputs, collapseLabel, positiveClass, sortQuestionName,
} from "../../src/prompt-sort/axes.js";

describe("axes", () => {
  it("has 11 axes, each with a typed Jev question within the API's limits", () => {
    expect(ALL_AXES).toHaveLength(11);
    expect(Object.keys(JEV_QUESTIONS).sort()).toEqual([...ALL_AXES].sort());
    for (const axis of ALL_AXES) {
      const q = JEV_QUESTIONS[axis];
      if (axis === "complexity") expect(q).toMatchObject({ type: "score" });
      else if (axis === "ambiguity") expect(q).toMatchObject({ type: "score" });
      else expect(q.type).toBe("noul");
    }
    expect((JEV_QUESTIONS.complexity as { criteria: string[] }).criteria).toHaveLength(4);
    expect((JEV_QUESTIONS.ambiguity as { criteria: string[] }).criteria).toHaveLength(3);
  });

  it("labels booleans yes/no and keeps levels as they are", () => {
    expect(axisLabel("is_task", true)).toBe("yes");
    expect(axisLabel("touches_ui", false)).toBe("no");
    expect(axisLabel("complexity", "large")).toBe("large");
    expect(axisOutputs("is_bug_report")).toEqual(["yes", "no"]);
    expect(axisOutputs("complexity")).toEqual(["trivial", "small", "substantial", "large"]);
    expect(axisOutputs("ambiguity")).toEqual(["clear", "partly", "vague"]);
  });

  it("collapses the two score axes to the class a scaffold acts on (RF-6)", () => {
    expect(axisClass("complexity", "substantial")).toBe("not-large");
    expect(axisClass("complexity", "large")).toBe("large");
    expect(axisClass("ambiguity", "partly")).toBe("clear");
    expect(axisClass("ambiguity", "vague")).toBe("unclear");
    expect(axisClass("is_task", true)).toBe("yes");
    expect(axisClass("is_task", false)).toBe("no");
    expect(positiveClass("complexity")).toBe("large");
    expect(positiveClass("ambiguity")).toBe("unclear");
    expect(positiveClass("touches_ui")).toBe("yes");
    expect(positiveClass("verification_defined")).toBe("no");
  });

  it("collapses fine and coarse labels into one space per eval question", () => {
    const c = sortQuestionName("complexity");
    expect(c).toBe("prompt-sort:complexity");
    expect(collapseLabel(c, "trivial")).toBe("not-large");
    expect(collapseLabel(c, "large")).toBe("large");
    expect(collapseLabel(c, "not-large")).toBe("not-large");
    expect(collapseLabel(sortQuestionName("ambiguity"), "partly")).toBe("clear");
    expect(collapseLabel(sortQuestionName("ambiguity"), "unclear")).toBe("unclear");
    expect(collapseLabel("prompt-sort:is_task", "yes")).toBe("yes");
    expect(Object.keys(AXIS_COLLAPSE).sort()).toEqual(["prompt-sort:ambiguity", "prompt-sort:complexity"]);
  });
});
