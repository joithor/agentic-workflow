// judge/tests/prompt-sort/heuristics.test.ts
import { describe, expect, it } from "vitest";

import { heuristicSort, isConfirmation } from "../../src/prompt-sort/heuristics.js";

describe("heuristicSort", () => {
  it("treats a confirmation as no task, trivial and clear", () => {
    expect(heuristicSort("yes go ahead")).toMatchObject({ is_task: false, complexity: "trivial", ambiguity: "clear", wants_loop: false });
  });

  it("treats a question as no task", () => {
    expect(heuristicSort("what does the done gate do?").is_task).toBe(false);
    expect(heuristicSort("should I fix the login button").is_task).toBe(false);
  });

  it("reads a task with a path, a loop trigger and a verification phrase", () => {
    const v = heuristicSort("Refactor src/auth/session.ts across the codebase and keep going until all tests pass");
    expect(v).toMatchObject({
      is_task: true, wants_loop: true, scope_defined: true, verification_defined: true,
      complexity: "substantial", ambiguity: "partly",
    });
  });

  it("flags an uncredited task as vague", () => {
    expect(heuristicSort("Fix the login button crash on the settings page")).toMatchObject({
      is_task: true, ambiguity: "vague", is_bug_report: true, touches_ui: true, scope_defined: false,
    });
  });

  it("detects bug reports, UI, research and limits independently of task shape", () => {
    expect(heuristicSort("The save button throws an error and the page crashes")).toMatchObject({ is_bug_report: true, touches_ui: true });
    expect(heuristicSort("investigate why the shift list is slow").needs_research).toBe(true);
    expect(heuristicSort("update Header.tsx only, no more than 20 lines").limits_defined).toBe(true);
    expect(heuristicSort("rewrite the parser using a state machine").approach_defined).toBe(true);
    expect(heuristicSort("edit `parseShift` and show a screenshot when done").scope_defined).toBe(true);
  });

  it("scores several complexity indicators as large", () => {
    expect(heuristicSort("Redesign and migrate the architecture across all files, implement the new feature").complexity).toBe("large");
  });

  it("returns clear/trivial/no-axes for an empty prompt", () => {
    expect(heuristicSort("   ")).toMatchObject({ is_task: false, complexity: "trivial", ambiguity: "clear", is_bug_report: false });
  });
});

describe("heuristicSort edge branches", () => {
  it("rates a medium-weight verb as small and does not treat punctuation as a question starter", () => {
    expect(heuristicSort("modify the readme").complexity).toBe("small");
    expect(heuristicSort("modify the readme").is_task).toBe(true);
    expect(heuristicSort("!!").is_task).toBe(false);
  });

  it("credits an acceptance phrase when scoring ambiguity", () => {
    expect(heuristicSort("update the app when done").ambiguity).toBe("partly");
    expect(heuristicSort("update the app when done").limits_defined).toBe(true);
  });
});

describe("isConfirmation", () => {
  it("accepts short confirmations and rejects longer text", () => {
    expect(isConfirmation("ok")).toBe(true);
    expect(isConfirmation("sounds good")).toBe(true);
    expect(isConfirmation("yes please fix the other thing in the file now")).toBe(false);
    expect(isConfirmation("")).toBe(false);
  });
});
