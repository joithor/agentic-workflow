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

describe("parsePlan fence variants (Review Focus 3)", () => {
  const BT = "`".repeat(3);
  const BT4 = "`".repeat(4);
  const doc = (...body: string[]): ReturnType<typeof parsePlan> => parsePlan(["# T", "### Task 1: Real", "- [ ] **Step 1: real**", ...body, "### Task 2: After", "- [x] **Step 1: after**"].join("\n"));
  const expectOnlyRealTasks = (plan: ReturnType<typeof parsePlan>): void => {
    expect(plan.tasks.map((t) => [t.number, t.stepsDone, t.stepsTotal])).toEqual([[1, 0, 1], [2, 1, 1]]);
  };

  it("treats a ~~~ fence as code", () => {
    const plan = doc("~~~", "### Task 9: fenced", "- [ ] **Step 1: fenced**", "~~~");
    expectOnlyRealTasks(plan);
    expect(plan.tasks[0].codeLines).toBe(2);
  });

  it("keeps a ``` fence inside a ~~~ fence as code, and the reverse", () => {
    const tildeOuter = doc("~~~", BT, "### Task 9: fenced", "- [ ] **Step 1: fenced**", BT, "- [ ] **Step 2: still fenced**", "~~~");
    expectOnlyRealTasks(tildeOuter);
    expect(tildeOuter.tasks[0].codeLines).toBe(5);
    const backtickOuter = doc(BT, "~~~", "### Task 9: fenced", "- [ ] **Step 1: fenced**", "~~~", "- [ ] **Step 2: still fenced**", BT);
    expectOnlyRealTasks(backtickOuter);
    expect(backtickOuter.tasks[0].codeLines).toBe(5);
  });

  it("closes a fence with a longer closer", () => {
    const plan = doc(BT, "### Task 9: fenced", "- [ ] **Step 1: fenced**", BT4, "- [ ] **Step 2: real, after the fence**");
    expect(plan.tasks.map((t) => [t.number, t.stepsDone, t.stepsTotal])).toEqual([[1, 0, 2], [2, 1, 1]]);
    expect(plan.tasks[0].codeLines).toBe(2);
  });

  it("does not let a shorter inner fence close the outer one", () => {
    const plan = doc(BT4, BT, "### Task 9: fenced", BT, "- [ ] **Step 1: still fenced**", BT4);
    expectOnlyRealTasks(plan);
    expect(plan.tasks[0].codeLines).toBe(4);
  });
});

describe("parsePlan line endings and inline code (PR #69 review)", () => {
  const BT = "`".repeat(3);

  it("parses a CRLF plan, with a leading BOM, the same as an LF plan", () => {
    const lf = ["# T", "### Task 1: A", "- [x] **Step 1: one**", BT, "code", BT, "### Task 2: B", "- [ ] **Step 1: two**"];
    const crlf = parsePlan("﻿" + lf.join("\r\n"));
    expect(crlf).toEqual(parsePlan(lf.join("\n")));
    expect(crlf.title).toBe("T");
    expect(crlf.tasks.map((t) => [t.number, t.title, t.stepsDone, t.stepsTotal, t.codeLines])).toEqual([[1, "A", 1, 1, 1], [2, "B", 0, 1, 0]]);
  });

  it("does not open a fence for inline triple backticks or a fence indented 4+ spaces", () => {
    const plan = parsePlan(["# T", "### Task 1: A", `${BT}code${BT} and text`, "### Task 2: B", `    ${BT}`, "### Task 3: C", "   " + BT, "### Task 9: fenced", "   " + BT].join("\n"));
    expect(plan.tasks.map((t) => [t.number, t.codeLines])).toEqual([[1, 0], [2, 0], [3, 1]]);
  });
});
