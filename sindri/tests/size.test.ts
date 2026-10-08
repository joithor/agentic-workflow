import { describe, expect, it } from "vitest";

import type { WorkItem } from "../src/adapters/types.js";
import { assess, nextPerPlan, sizeByRules, startBlocker } from "../src/observe/size.js";
import { ProfileSchema } from "../src/profile/schema.js";

const profile = ProfileSchema.parse({
  schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"], trustedAuthors: ["me@example.com"],
});

const item = (id: string, meta: Record<string, string | number>, over: Partial<WorkItem> = {}): WorkItem => ({
  id, title: id, body: "", url: "", state: "open", authors: [{ id: "me@example.com", role: "creator" }], updatedAt: "", meta, ...over,
});

describe("rule-based sizing", () => {
  it.each([
    [1, 40, "XS"], [1, 41, "S"], [3, 200, "S"], [4, 10, "M"], [6, 500, "M"], [10, 1000, "L"], [11, 0, "XL"], [1, 5000, "XL"],
  ])("%i files, %i code lines → %s", (files, lines, size) => {
    expect(sizeByRules(files, lines)).toBe(size);
  });

  it("assesses size, ambiguity and trust from meta and authors", () => {
    const full = item("a.t1", { files: 1, codeLines: 10, stepsTotal: 3, hasFilesBlock: 1 });
    expect(assess(full, profile)).toEqual({ size: "XS", sizedBy: "rules", ambiguity: "none", trusted: true });
    expect(assess(item("a.t2", {}), profile)).toEqual({ size: null, sizedBy: "rules", ambiguity: "unknown", trusted: true });
    expect(assess(item("a.t5", { codeLines: 300 }), profile).size).toBe("M");
    expect(assess(item("a.t3", {}, { authors: [] }), profile).trusted).toBe(false);
    expect(assess(item("a.t4", {}, { authors: [{ id: "x@example.com", role: "editor" }] }), profile).trusted).toBe(false);
  });

  it("finds the first open task of each plan", () => {
    const items = [
      item("a.t2", { plan: "a", order: 2 }),
      item("a.t1", { plan: "a", order: 1 }, { state: "done" }),
      item("a.t3", { plan: "a", order: 3 }),
      item("b.t1", { plan: "b", order: 1001 }),
      item("loose", {}),
    ];
    expect([...nextPerPlan(items)].sort()).toEqual(["a.t2", "b.t1", "loose"]);
  });

  it("names why auto-small would not start an item", () => {
    const a = { size: "XS" as const, sizedBy: "rules" as const, ambiguity: "none" as const, trusted: true };
    const open = item("x", {});
    expect(startBlocker(open, a, true, "XS")).toBeNull();
    expect(startBlocker({ ...open, state: "done" }, a, true, "XS")).toBe("done");
    expect(startBlocker(open, a, false, "XS")).toBe("waits on earlier task");
    expect(startBlocker(open, { ...a, size: null }, true, "XS")).toBe("unsized");
    expect(startBlocker(open, { ...a, size: "S" }, true, "XS")).toBe("size > XS");
    expect(startBlocker(open, { ...a, ambiguity: "unknown" }, true, "XS")).toBe("unclear");
    expect(startBlocker(open, { ...a, trusted: false }, true, "XS")).toBe("untrusted author");
  });
});
