// judge/tests/prompt-sort/tier1.test.ts
import { describe, expect, it } from "vitest";

import { tier1Skip } from "../../src/prompt-sort/tier1.js";

describe("tier1Skip (RF-3: nothing machine-made or trivial reaches Jev)", () => {
  it.each([
    ["", "empty"],
    ["   \n", "empty"],
    ["ok", "too-short"],
    ["<system-reminder>x</system-reminder>", "machine"],
    ["<teammate-message teammate_id=\"a\">hi</teammate-message>", "machine"],
    ["Another Claude session sent a message: done", "machine"],
    ["Base directory for this skill: /x", "machine"],
    ["Caveat: The messages below were generated", "machine"],
    ["This session is being continued from a previous conversation", "machine"],
    ["[Request interrupted by user]", "machine"],
    ["/bugFixOrchestrator FRN-123", "slash-command"],
    ["/clear", "slash-command"],
    ["yes go ahead", "confirmation"],
    ["sounds good", "confirmation"],
  ])("%j -> %s", (prompt, reason) => {
    expect(tier1Skip(prompt)).toBe(reason);
  });

  it("lets a pasted absolute path and real prompts through", () => {
    expect(tier1Skip("/Users/joi/app/src/a.ts throws on save")).toBeNull();
    expect(tier1Skip("Fix the login button crash on the settings page")).toBeNull();
    expect(tier1Skip("what does the done gate do?")).toBeNull();
  });
});
