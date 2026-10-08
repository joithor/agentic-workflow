import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import type { HumanTurn } from "../src/audit/human-turns.js";
import { itemIdsForSession, sessionTokenTotals, summarizeItemUsage } from "../src/audit/usage.js";

const ID = /[A-Z][A-Z0-9]{1,9}-\d+/g;
const t = (text: string): HumanTurn => ({ project: "p", session: "s", ts: "", index: 1, kind: "turn", text, skills: [], guardFiredBefore: false, compactedBefore: false, editsBefore: false, contextTokens: 0, prevAssistantTail: "" });

describe("sessionTokenTotals", () => {
  it("sums usage once per message id", async () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "usage-"));
    const file = path.join(dir, "s.jsonl");
    const usage = { input_tokens: 10, cache_read_input_tokens: 100, cache_creation_input_tokens: 5, output_tokens: 7 };
    fs.writeFileSync(file, [
      { type: "assistant", message: { id: "m1", usage } },
      { type: "assistant", message: { id: "m1", usage } },
      { type: "assistant", message: { id: "m2", usage } },
    ].map((l) => JSON.stringify(l)).join("\n"));
    expect(await sessionTokenTotals(file)).toEqual({ input: 20, cacheRead: 200, cacheCreation: 10, output: 14 });
  });

  it("skips malformed, non-object, non-assistant and id-less lines and tolerates missing or non-numeric usage", async () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "usage-"));
    const file = path.join(dir, "s.jsonl");
    fs.writeFileSync(file, [
      "not json",
      "null",
      "7",
      "[1]",
      JSON.stringify({ type: "user", message: { id: "u1", usage: { input_tokens: 99 } } }),
      JSON.stringify({ type: "assistant", message: { usage: { input_tokens: 99 } } }),
      JSON.stringify({ type: "assistant", message: { id: "m1" } }),
      JSON.stringify({ type: "assistant", message: { id: "m2", usage: { input_tokens: "x", output_tokens: 3 } } }),
    ].join("\n"));
    expect(await sessionTokenTotals(file)).toEqual({ input: 0, cacheRead: 0, cacheCreation: 0, output: 3 });
  });
});

describe("itemIdsForSession", () => {
  it("returns distinct ids in first-seen order", () => {
    expect(itemIdsForSession([t("fix ABC-12 and ABC-9"), t("also ABC-12")], ID)).toEqual(["ABC-12", "ABC-9"]);
  });

  it("works with a pattern that has no global flag", () => {
    expect(itemIdsForSession([t("see ABC-1 and ABC-2")], /[A-Z][A-Z0-9]{1,9}-\d+/)).toEqual(["ABC-1", "ABC-2"]);
  });
});

describe("summarizeItemUsage", () => {
  it("splits a session's tokens across its items and reports median and p75", () => {
    const s = summarizeItemUsage([
      { session: "a", items: ["X-1"], total: 100 },
      { session: "b", items: ["X-2", "X-3"], total: 200 },
      { session: "c", items: [], total: 999 },
    ]);
    expect(s.byItem).toEqual({ "X-1": 100, "X-2": 100, "X-3": 100 });
    expect(s).toMatchObject({ items: 3, medianTokens: 100, p75Tokens: 100 });
  });

  it("accumulates an item across sessions and returns zeros when there are no items", () => {
    expect(summarizeItemUsage([{ session: "a", items: ["X-1"], total: 10 }, { session: "b", items: ["X-1"], total: 5 }]).byItem).toEqual({ "X-1": 15 });
    expect(summarizeItemUsage([])).toEqual({ items: 0, medianTokens: 0, p75Tokens: 0, byItem: {} });
  });
});
