import { describe, expect, it } from "vitest";

import { parsePlan } from "../src/adapters/plan-file/parse.js";

const PLAN = [
  "# Demo Plan",
  "",
  "## Global Constraints",
  "- [ ] not a step (outside any task)",
  "",
  "### Task 1: First thing",
  "",
  "**Files:**",
  "- Create: `src/a.ts`, `src/b.ts`",
  "- Modify: `config/x.sh:57-60` (the regex line)",
  "- Test: `tests/a.test.ts`",
  "",
  "**Interfaces:** none",
  "",
  "- [x] **Step 1: Write the failing test**",
  "",
  "```ts",
  "const a = 1;",
  "// - [ ] a checkbox inside code is not a step",
  "```",
  "",
  "- [ ] **Step 2: Implement**",
  "",
  "### Task 2: Template example",
  "",
  "````markdown",
  "### Task 9: inside a fence, not a task",
  "- [ ] **Step 1: not a step**",
  "```python",
  "x = 1",
  "```",
  "````",
  "",
  "- [X] **Step 1: Only real step**",
  "",
  "## Done criteria",
  "- [ ] not a step either",
].join("\n");

describe("parsePlan (Review Focus 3)", () => {
  const plan = parsePlan(PLAN);

  it("reads the title and only real task headings", () => {
    expect(plan.title).toBe("Demo Plan");
    expect(plan.tasks.map((t) => [t.number, t.title])).toEqual([[1, "First thing"], [2, "Template example"]]);
  });

  it("counts checkbox steps outside fences only, including [X]", () => {
    expect(plan.tasks.map((t) => [t.stepsDone, t.stepsTotal])).toEqual([[1, 2], [1, 1]]);
  });

  it("reads the Files block, strips line ranges, and stops at the next paragraph", () => {
    expect(plan.tasks[0].files).toEqual(["src/a.ts", "src/b.ts", "config/x.sh", "tests/a.test.ts"]);
    expect(plan.tasks[0].hasFilesBlock).toBe(true);
    expect(plan.tasks[1].hasFilesBlock).toBe(false);
  });

  it("counts code lines inside fences, including nested fence lines", () => {
    expect(plan.tasks[0].codeLines).toBe(2);
    expect(plan.tasks[1].codeLines).toBe(5);
  });

  it("ends a task body at a level-2 heading", () => {
    expect(plan.tasks[1].body).not.toContain("Done criteria");
    expect(plan.tasks[1].body).toContain("Only real step");
  });

  it("handles a plan with no title or tasks, and an unclosed fence", () => {
    expect(parsePlan("no headings here")).toEqual({ title: "", tasks: [] });
    const open = parsePlan("# T\n### Task 1: A\n```\n### Task 2: B\n");
    expect(open.tasks.map((t) => t.number)).toEqual([1]);
  });
});
