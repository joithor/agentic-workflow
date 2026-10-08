import { describe, expect, it } from "vitest";

import type { HumanTurn } from "../src/audit/human-turns.js";
import { countPatterns, PATTERNS } from "../src/audit/patterns.js";

const turn = (text: string, session = "s1"): HumanTurn => ({
  project: "p", session, ts: "t", index: 1, kind: "turn", text, skills: [], guardFiredBefore: false, compactedBefore: false, editsBefore: false, contextTokens: 0, prevAssistantTail: "",
});

describe("PATTERNS", () => {
  it.each([
    ["ship_recipe", "raise the PR as draft, run /review + /addressReview and monitor bugbot"],
    ["push_only", "push"],
    ["evidence_env", "attach the screenshots to the linear issue"],
    ["image_turn", "[Image #3] this still wraps onto a second row"],
    ["handoff", "give me a handoff prompt for a new orchestrator"],
    ["restate", "The ticket description suggests the fix. why are we not doing it"],
    ["rigor", "refute or confirm WITH EVIDENCE"],
    ["dispatch", "fix this https://linear.app/acme/issue/ABC-12/thing"],
  ] as const)("%s matches a representative turn", (name, text) => {
    expect(PATTERNS[name].test(text)).toBe(true);
  });

  it("push_only does not match longer instructions", () => {
    expect(PATTERNS.push_only.test("push the fix and then rebase the stack")).toBe(false);
  });
});

describe("countPatterns", () => {
  it("counts turns and distinct sessions per pattern", () => {
    const counts = countPatterns([turn("push", "a"), turn("push", "a"), turn("push", "b"), turn("hello", "c")]);
    expect(counts.push_only).toEqual({ turns: 3, sessions: 2 });
    expect(counts.handoff).toEqual({ turns: 0, sessions: 0 });
  });

  it("ignores interrupt turns", () => {
    const counts = countPatterns([{ ...turn("push", "a"), kind: "interrupt" }]);
    expect(counts.push_only).toEqual({ turns: 0, sessions: 0 });
  });
});
