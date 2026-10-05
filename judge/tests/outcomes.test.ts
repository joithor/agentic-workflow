import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { labeledItems, openDb, recordDecision, recordDecisionDetails, upsertEvalItem } from "../src/db.js";
import { askCheckOutcome, classifyReply, findTranscript, realPrompts, runOutcomeLabels } from "../src/outcomes.js";

const jsonl = fs.readFileSync(path.join(import.meta.dirname, "fixtures", "ask-outcome.jsonl"), "utf8");

describe("realPrompts", () => {
  it("keeps only real user prompts, in order, skipping tool results, machine text and bad lines", () => {
    expect(realPrompts(`garbage\n\n${jsonl}`)).toEqual([
      { ts: "2026-10-03T10:00:00.000Z", text: "Refactor chain.ts" },
      { ts: "2026-10-03T10:10:00.000Z", text: "no, don't touch the tests" },
    ]);
  });

  it("excludes a compaction-summary prompt, so the ask-check labeler never reads it as the next reply", () => {
    const summary = "This session is being continued from a previous conversation that ran out of context.";
    const lines = [
      { type: "user", timestamp: "2026-10-03T10:00:00.000Z", message: { content: "do it" } },
      { type: "user", timestamp: "2026-10-03T10:05:00.000Z", message: { content: summary } },
      { type: "user", timestamp: "2026-10-03T10:06:00.000Z", message: { content: [{ type: "text", text: `  ${summary}` }] } },
    ].map((l) => JSON.stringify(l)).join("\n");
    const prompts = realPrompts(lines);
    expect(prompts).toEqual([{ ts: "2026-10-03T10:00:00.000Z", text: "do it" }]);
    expect(askCheckOutcome("continue", "2026-10-03T10:01:00.000Z", prompts)).toBeNull();
  });

  it("skips non-user lines, missing timestamps and non-text content", () => {
    const lines = [
      { type: "assistant", timestamp: "2026-10-03T10:00:00.000Z", message: { content: "hi" } },
      { type: "user", message: { content: "no timestamp" } },
      { type: "user", timestamp: "2026-10-03T10:00:00.000Z", message: { content: 5 } },
      { type: "user", timestamp: "2026-10-03T10:00:00.000Z", message: { content: [{ type: "image" }] } },
      { type: "user", timestamp: "2026-10-03T10:00:00.000Z" },
      { type: "user", timestamp: "2026-10-03T10:01:00.000Z", message: { content: [{ type: "text" }, { type: "text", text: "a" }] } },
    ].map((l) => JSON.stringify(l)).join("\n");
    expect(realPrompts(lines)).toEqual([{ ts: "2026-10-03T10:01:00.000Z", text: "\na" }]);
  });
});

describe("classifyReply", () => {
  it.each([
    ["[Request interrupted by user]", "interrupt"],
    ["no, don't touch the tests", "correction"],
    ["Actually use the other file", "correction"],
    ["continue", "bare-continue"],
    ["go ahead!", "bare-continue"],
    ["now add a test for buildChain", "other"],
  ])("%s -> %s", (text, kind) => expect(classifyReply(text)).toBe(kind));
});

describe("askCheckOutcome", () => {
  const p = (ts: string, text: string) => ({ ts, text });
  it("labels a continue followed by a correction as ask", () => {
    expect(askCheckOutcome("continue", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:00:00.000Z", "x"), p("2026-10-03T10:10:00.000Z", "no, stop")])).toBe("ask");
  });
  it("labels an ask answered with a bare continue as continue", () => {
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:02:00.000Z", "yes")])).toBe("continue");
  });
  it("confirms the action when the next prompt is ordinary", () => {
    expect(askCheckOutcome("continue", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:30:00.000Z", "now add docs")])).toBe("continue");
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:30:00.000Z", "use option B")])).toBe("ask");
  });
  it("gives no label without a next prompt within 6h", () => {
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [])).toBeNull();
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T17:00:00.000Z", "yes")])).toBeNull();
  });
});

describe("findTranscript", () => {
  it("fails open: a missing projects dir yields undefined", () => {
    expect(findTranscript(path.join(os.tmpdir(), "definitely-missing-projects-dir-xyz"), "s1")).toBeUndefined();
  });
});

describe("runOutcomeLabels", () => {
  const dec = (id: string, ts: string) => ({ id, ts, question: "ask-check", content_class: "transcript", provider: "jev", decision: "continue", confidence: 0.9, reason_code: "jev", latency_ms: 1, input_digest: "d", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const });
  const seed = (db: ReturnType<typeof openDb>, id: string, sessionId: string | null): void => {
    recordDecision(db, dec(id, "2026-10-03T10:01:00.000Z"));
    recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement: null, session_id: sessionId });
    upsertEvalItem(db, { id: `i-${id}`, question: "ask-check", input_json: "{}", source: `decision:${id}`, model_decision: "continue", created_at: "2026-10-03T11:00:00.000Z" });
  };
  const now = () => new Date("2026-10-04T00:00:00.000Z");

  it("labels imported ask-check items from their session transcript and counts items without signal", () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    fs.mkdirSync(path.join(projects, "-repo"));
    fs.writeFileSync(path.join(projects, "-repo", "s1.jsonl"), jsonl);
    expect(findTranscript(projects, "s1")).toBe(path.join(projects, "-repo", "s1.jsonl"));
    expect(findTranscript(projects, "nope")).toBeUndefined();

    const db = openDb(":memory:");
    seed(db, "d1", "s1");
    seed(db, "d2", null);
    expect(runOutcomeLabels(db, { projectsDir: projects, now })).toEqual({ labeled: 1, noSignal: 1 });
    expect(labeledItems(db, "ask-check", "outcome").map((r) => [r.id, r.label])).toEqual([["i-d1", "ask"]]);
    // Re-running does not relabel the already-labeled item.
    expect(runOutcomeLabels(db, { projectsDir: projects, now })).toEqual({ labeled: 0, noSignal: 1 });
  });

  it("fails open when the transcript cannot be read", () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    fs.mkdirSync(path.join(projects, "-repo"));
    fs.writeFileSync(path.join(projects, "-repo", "s1.jsonl"), jsonl);
    const db = openDb(":memory:");
    seed(db, "d1", "s1");
    const readFile = (): string => {
      throw new Error("EACCES");
    };
    expect(runOutcomeLabels(db, { projectsDir: projects, now, readFile })).toEqual({ labeled: 0, noSignal: 1 });
  });
});
