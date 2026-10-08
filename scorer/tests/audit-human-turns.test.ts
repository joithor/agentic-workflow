import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { extractHumanTurns } from "../src/audit/human-turns.js";

function writeJsonl(lines: unknown[], extra = ""): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "audit-"));
  const file = path.join(dir, "s1.jsonl");
  fs.writeFileSync(file, lines.map((l) => JSON.stringify(l)).join("\n") + "\n" + extra);
  return file;
}

async function collect(file: string) {
  const out = [];
  for await (const t of extractHumanTurns({ path: file, project: "p", sessionId: "s1" })) out.push(t);
  return out;
}

const user = (text: string, extra: Record<string, unknown> = {}) => ({ type: "user", timestamp: "2026-10-01T00:00:00Z", message: { content: text }, ...extra });
const assistantTool = (name: string, extra: Record<string, unknown> = {}) => ({
  type: "assistant",
  ...extra,
  message: { usage: { input_tokens: 1 }, content: [{ type: "tool_use", name, input: {} }] },
});
const assistant = (text: string, input = 1000, skill?: string) => ({
  type: "assistant",
  message: {
    usage: { input_tokens: input, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 },
    content: [{ type: "text", text }, ...(skill ? [{ type: "tool_use", name: "Skill", input: { skill } }] : [])],
  },
});

describe("extractHumanTurns", () => {
  it("keeps typed human turns and drops machine text, meta, sidechain and tool results", async () => {
    const file = writeJsonl([
      user("<system-reminder>x</system-reminder>"),
      user("meta", { isMeta: true }),
      user("side", { isSidechain: true }),
      { type: "user", timestamp: "t", message: { content: [{ type: "tool_result", content: "ok" }] } },
      assistant("I can open the PR. Want me to?", 5000, "bugFixOrchestrator"),
      user("raise the PR as draft, run /review + /addressReview"),
    ]);
    const turns = await collect(file);
    expect(turns).toHaveLength(1);
    expect(turns[0]).toMatchObject({ kind: "turn", index: 1, skills: ["bugFixOrchestrator"], contextTokens: 5000, prevAssistantTail: "I can open the PR. Want me to?" });
  });

  it("records slash commands with their args and interrupts", async () => {
    const file = writeJsonl([
      user("<command-name>/bugFixOrchestrator</command-name><command-args>ABC-12</command-args>"),
      user("[Request interrupted by user]"),
    ]);
    const turns = await collect(file);
    expect(turns.map((t) => [t.kind, t.text])).toEqual([["command", "/bugFixOrchestrator ABC-12"], ["interrupt", "[Request interrupted by user]"]]);
    expect(turns[1].skills).toContain("bugFixOrchestrator");
  });

  it("marks guard-fired and compacted state for later turns", async () => {
    const file = writeJsonl([
      { type: "attachment", text: "context is now ~210000 tokens (past the 200000-token guard)" },
      user("This session is being continued from a previous conversation"),
      user("continue the plan"),
    ]);
    const [turn] = await collect(file);
    expect(turn).toMatchObject({ guardFiredBefore: true, compactedBefore: true, text: "continue the plan" });
  });

  it.each(["Edit", "Write", "MultiEdit", "NotebookEdit"])("sets editsBefore after a main-chain %s tool_use", async (tool) => {
    const file = writeJsonl([user("one"), assistantTool("Read"), user("two"), assistantTool(tool), user("three")]);
    const turns = await collect(file);
    expect(turns.map((t) => t.editsBefore)).toEqual([false, false, true]);
  });

  it("ignores edit tool_use inside sidechains when setting editsBefore", async () => {
    const file = writeJsonl([user("one"), assistantTool("Edit", { isSidechain: true }), user("two")]);
    expect((await collect(file)).map((t) => t.editsBefore)).toEqual([false, false]);
  });

  it("skips malformed and truncated lines without throwing", async () => {
    const file = writeJsonl([user("first")], "{not json\n{\"type\":\"user\",\"message\":{\"content\":\"trunc");
    const turns = await collect(file);
    expect(turns.map((t) => t.text)).toEqual(["first"]);
  });

  it("streams a large file without loading it whole", async () => {
    const lines = Array.from({ length: 50_000 }, (_, i) => user(`turn ${i}`));
    const turns = await collect(writeJsonl(lines));
    expect(turns).toHaveLength(50_000);
  });

  it("tolerates unusual record shapes and omitted fields", async () => {
    const file = writeJsonl([
      { type: "user", message: { content: 42 } },
      { type: "user", message: { content: [null, { type: "text", text: "from blocks" }] } },
      { type: "assistant", message: { content: "plain string", usage: { input_tokens: "x" } } },
      { type: "assistant" },
      { type: "assistant", message: { content: [null, { type: "tool_use", name: 7 }, { type: "tool_use", name: "Skill", input: null }, { type: "tool_use", name: "Skill", input: { skill: 1 } }] } },
      { type: "mystery" },
      user("<command-name>plain</command-name>"),
      user("<command-name>/plain</command-name><command-args>again</command-args>"),
      { type: "user", message: { content: [{ type: "tool_result", content: "x" }, { type: "text", text: "typed alongside" }] } },
    ]);
    const turns = await collect(file);
    expect(turns.map((t) => t.text)).toEqual(["from blocks", "/plain", "/plain again", "typed alongside"]);
    expect(turns[0]).toMatchObject({ ts: "", contextTokens: 0, editsBefore: false, skills: [] });
    expect(turns[2].skills).toEqual(["plain"]);
  });

  it("skips JSON lines that are not objects", async () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "audit-"));
    const file = path.join(dir, "s1.jsonl");
    fs.writeFileSync(file, [JSON.stringify(user("first")), "null", "42", '"str"', "[]", "true", JSON.stringify(user("second"))].join("\n") + "\n");
    expect((await collect(file)).map((t) => t.text)).toEqual(["first", "second"]);
  });
});
