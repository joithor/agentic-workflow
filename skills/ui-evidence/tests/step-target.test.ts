import { describe, expect, it } from "vitest";

import { isPrefixedTarget, parseStepTarget, targetLabel } from "../src/step-target.js";

describe("parseStepTarget", () => {
  it("keeps a bare target as a test id", () => {
    expect(parseStepTarget("save-btn")).toEqual({ kind: "testId", testId: "save-btn" });
  });
  it("parses role:<role>|<name>, keeping a | inside the name", () => {
    expect(parseStepTarget("role:button|Save")).toEqual({ kind: "role", role: "button", name: "Save" });
    expect(parseStepTarget("role:option|A | B")).toEqual({ kind: "role", role: "option", name: "A | B" });
  });
  it("parses text:<text>", () => {
    expect(parseStepTarget("text:Unit 4200")).toEqual({ kind: "text", text: "Unit 4200" });
  });
  it("reports malformed prefixed targets", () => {
    for (const bad of ["role:button", "role:|x", "role:button|", "text:", "role:button|  ", "text:  ", "role:Button|Save", "Role:button|Save", " text:Save"]) expect(parseStepTarget(bad)).toHaveProperty("error");
  });
  it("labels prefixed targets by their name or text", () => {
    expect(targetLabel("role:button|Save")).toBe("Save");
    expect(targetLabel("text:Delete")).toBe("Delete");
    expect(targetLabel("save-btn")).toBe("save-btn");
    expect(isPrefixedTarget("text:x")).toBe(true);
    expect(isPrefixedTarget("x")).toBe(false);
  });
});
