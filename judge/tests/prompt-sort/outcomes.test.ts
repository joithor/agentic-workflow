import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { labeledItems, openDb, upsertEvalItem } from "../../src/db.js";
import { blend } from "../../src/prompt-sort/blend.js";
import { heuristicSort } from "../../src/prompt-sort/heuristics.js";
import { importSortItems } from "../../src/prompt-sort/import.js";
import { LARGE_EDITS, LARGE_FILES, runSortOutcomeLabels, sortOutcomeLabel, windowFacts, type WindowFacts } from "../../src/prompt-sort/outcomes.js";
import type { SortOutcome } from "../../src/prompt-sort/sort.js";
import { recordPromptSort } from "../../src/prompt-sort/store.js";

const jsonl = fs.readFileSync(path.join(import.meta.dirname, "..", "fixtures", "sort-outcome.jsonl"), "utf8");
const DECIDED = "2026-10-03T10:00:00.000Z";
const facts = (over: Partial<WindowFacts> = {}): WindowFacts => ({ skills: [], editedFiles: [], editCalls: 0, assistantTurns: 0, nextPrompts: [], closed: false, ...over });

describe("windowFacts", () => {
  it("collects edits, skills (tool calls and slash commands) and the next prompts up to the next new-task prompt", () => {
    const f = windowFacts(`garbage\n\n${jsonl}`, DECIDED);
    expect(f.editedFiles.sort()).toEqual(["/r/web/src/components/LoginButton.tsx", "/r/web/src/components/SignupButton.tsx", "/r/web/src/lib/session.ts"]);
    expect(f.editCalls).toBe(3);
    expect(f.skills.sort()).toEqual(["bugfixorchestrator", "verify-web"]);
    expect(f.assistantTurns).toBe(4);
    expect(f.closed).toBe(true);
  });

  it("C11: truncates nextPrompts at the topic change so another task's prompts never label this one", () => {
    const f = windowFacts(jsonl, DECIDED);
    expect(f.nextPrompts.map((p) => p.text)).toEqual(["no, i meant the signup button"]);
    // Without a topic change both prompts stay in the window.
    const open = windowFacts(
      [
        jsonl.split("\n")[0],
        '{"type":"user","timestamp":"2026-10-03T10:04:00.000Z","message":{"content":"looks fine"}}',
        '{"type":"user","timestamp":"2026-10-03T10:05:00.000Z","message":{"content":"add a test"}}',
        '{"type":"user","timestamp":"2026-10-03T10:06:00.000Z","message":{"content":"and a third one"}}',
      ].join("\n"),
      DECIDED,
    );
    expect(open.nextPrompts.map((p) => p.text)).toEqual(["looks fine", "add a test"]);
    expect(open.closed).toBe(false);
  });

  it("is not closed when no later prompt changes the topic, and ignores lines before the decision or after 6h", () => {
    const f = windowFacts(jsonl.split("\n").slice(0, 8).join("\n"), DECIDED);
    expect(f.closed).toBe(false);
    expect(f.editCalls).toBe(3);
    expect(windowFacts(jsonl, "2026-10-03T23:00:00.000Z")).toMatchObject({ editCalls: 0, nextPrompts: [] });
  });

  it("tolerates odd row shapes (no content, non-array/non-string content, text blocks, notebook edits, untimed rows)", () => {
    const rows = [
      { type: "assistant", timestamp: "2026-10-03T10:00:10.000Z", message: {} },
      { type: "assistant", timestamp: "2026-10-03T10:00:11.000Z", message: { content: "plain" } },
      { type: "assistant", timestamp: "2026-10-03T10:00:12.000Z", message: { content: [{ type: "text", text: "hi" }, { type: "tool_use", name: 5 }, { type: "tool_use", name: "NotebookEdit", input: { notebook_path: "/r/n.ipynb" } }, { type: "tool_use", name: "Write" }, { type: "tool_use", name: "Skill", input: {} }, { type: "tool_use", name: "Bash" }] } },
      { type: "user", timestamp: "2026-10-03T10:00:13.000Z", message: { content: 7 } },
      { type: "user", timestamp: "2026-10-03T10:00:14.000Z", message: {} },
      { type: "user", timestamp: "2026-10-03T10:00:15.000Z", message: { content: [{ type: "text", text: "<command-name>rootCause</command-name>" }, { type: "image" }, { type: "text" }] } },
      { type: "system", timestamp: "2026-10-03T10:00:16.000Z" },
      { type: "assistant" },
    ];
    const f = windowFacts(rows.map((r) => JSON.stringify(r)).join("\n"), DECIDED);
    expect(f).toMatchObject({ assistantTurns: 3, editCalls: 2, editedFiles: ["/r/n.ipynb"], skills: ["rootcause"] });
  });
});

describe("sortOutcomeLabel (fixed rules)", () => {
  it("is_task: edits -> yes; no edits but a reply -> no; nothing -> no label", () => {
    expect(sortOutcomeLabel("is_task", facts({ editCalls: 1 }))).toBe("yes");
    expect(sortOutcomeLabel("is_task", facts({ assistantTurns: 2 }))).toBe("no");
    expect(sortOutcomeLabel("is_task", facts())).toBeNull();
  });
  it("is_bug_report is recall-only: a bug skill -> yes, otherwise no label", () => {
    expect(sortOutcomeLabel("is_bug_report", facts({ skills: ["rootcause"] }))).toBe("yes");
    expect(sortOutcomeLabel("is_bug_report", facts({ skills: ["verify-web"], editCalls: 4, assistantTurns: 4 }))).toBeNull();
  });
  it("touches_ui: UI file or UI skill -> yes; edits without UI -> no; no edits -> no label", () => {
    expect(sortOutcomeLabel("touches_ui", facts({ editedFiles: ["/r/a/Header.tsx"], editCalls: 1 }))).toBe("yes");
    expect(sortOutcomeLabel("touches_ui", facts({ editedFiles: ["/r/ios/Views/ShiftRow.swift"], editCalls: 1 }))).toBe("yes");
    expect(sortOutcomeLabel("touches_ui", facts({ skills: ["ui-evidence"] }))).toBe("yes");
    expect(sortOutcomeLabel("touches_ui", facts({ editedFiles: ["/r/server/db.ts", "/r/ios/Models/Shift.swift"], editCalls: 2 }))).toBe("no");
    expect(sortOutcomeLabel("touches_ui", facts({ assistantTurns: 3 }))).toBeNull();
  });
  it("complexity: >=8 files or >=25 edits -> large; else not-large once there was any work", () => {
    expect(LARGE_FILES).toBe(8);
    expect(LARGE_EDITS).toBe(25);
    expect(sortOutcomeLabel("complexity", facts({ editedFiles: Array.from({ length: 8 }, (_, i) => `/f${i}`), editCalls: 8 }))).toBe("large");
    expect(sortOutcomeLabel("complexity", facts({ editedFiles: ["/a"], editCalls: 25 }))).toBe("large");
    expect(sortOutcomeLabel("complexity", facts({ editedFiles: ["/a"], editCalls: 3, assistantTurns: 5 }))).toBe("not-large");
    expect(sortOutcomeLabel("complexity", facts())).toBeNull();
  });
  it("ambiguity: a correction, interrupt or clarification in the next 2 prompts -> unclear; otherwise clear; no next prompt -> no label", () => {
    const p = (text: string) => ({ ts: "2026-10-03T10:04:00.000Z", text });
    expect(sortOutcomeLabel("ambiguity", facts({ nextPrompts: [p("no, i meant the signup button")] }))).toBe("unclear");
    expect(sortOutcomeLabel("ambiguity", facts({ nextPrompts: [p("looks fine"), p("[Request interrupted by user]")] }))).toBe("unclear");
    expect(sortOutcomeLabel("ambiguity", facts({ nextPrompts: [p("looks fine"), p("add a test for it")] }))).toBe("clear");
    expect(sortOutcomeLabel("ambiguity", facts())).toBeNull();
  });
  it("has no outcome signal for the remaining axes", () => {
    for (const axis of ["wants_loop", "scope_defined", "limits_defined", "approach_defined", "verification_defined", "needs_research"] as const) {
      expect(sortOutcomeLabel(axis, facts({ editCalls: 9, assistantTurns: 9 }))).toBeNull();
    }
  });
});

describe("runSortOutcomeLabels (RF-5)", () => {
  const PROMPT = "Fix the login button crash on the settings page";
  const outcome = (): SortOutcome => ({ ...blend(heuristicSort(PROMPT), null), mode: "heuristic-only", reason: "no-jev", failure: null, latencyMs: 1, usage: null, sentPrompt: PROMPT });
  function setup(ts: string, sessionId: string | null) {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    fs.mkdirSync(path.join(projects, "-repo"));
    fs.writeFileSync(path.join(projects, "-repo", "s1.jsonl"), jsonl);
    const db = openDb(":memory:");
    recordPromptSort(db, { id: "d1", ts, sessionId, rawPrompt: PROMPT, outcome: outcome(), wouldFire: [], fired: [], suppressed: [] });
    importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" });
    return { db, projects };
  }
  const labels = (db: ReturnType<typeof openDb>, axis: string) => labeledItems(db, `prompt-sort:${axis}`, "outcome").map((r) => r.label);

  it("labels each outcome-capable axis from the session transcript once the window closed", () => {
    const { db, projects } = setup(DECIDED, "s1");
    const r = runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-03T10:30:00.000Z") });
    expect(r.labeled).toBe(5);
    expect(labels(db, "is_task")).toEqual(["yes"]);
    expect(labels(db, "is_bug_report")).toEqual(["yes"]);
    expect(labels(db, "touches_ui")).toEqual(["yes"]);
    expect(labels(db, "complexity")).toEqual(["not-large"]);
    expect(labels(db, "ambiguity")).toEqual(["unclear"]);
    expect(labels(db, "wants_loop")).toEqual([]);
    expect(runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-03T10:30:00.000Z") }).labeled).toBe(0);
  });

  it("holds back negative labels while the window is open and the decision is under 6h old, but keeps positive ones", () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    fs.mkdirSync(path.join(projects, "-repo"));
    fs.writeFileSync(path.join(projects, "-repo", "s1.jsonl"), jsonl.split("\n").slice(0, 3).join("\n"));
    const db = openDb(":memory:");
    recordPromptSort(db, { id: "d1", ts: DECIDED, sessionId: "s1", rawPrompt: PROMPT, outcome: outcome(), wouldFire: [], fired: [], suppressed: [] });
    importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" });
    const r = runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-03T10:30:00.000Z") });
    expect(labels(db, "is_task")).toEqual(["yes"]);
    expect(labels(db, "complexity")).toEqual([]);
    expect(r.pending).toBeGreaterThan(0);
    const later = runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-03T17:00:00.000Z") });
    expect(later.labeled).toBeGreaterThan(0);
    expect(labels(db, "complexity")).toEqual(["not-large"]);
  });

  it("counts items without a session or transcript as no signal", () => {
    const { db, projects } = setup(DECIDED, null);
    expect(runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-04T00:00:00.000Z") })).toEqual({ labeled: 0, noSignal: 11, pending: 0 });
    const missing = setup(DECIDED, "no-such-session");
    expect(runSortOutcomeLabels(missing.db, { projectsDir: missing.projects, now: () => new Date("2026-10-04T00:00:00.000Z") }).labeled).toBe(0);
  });

  it("matches items to their decision exactly, even when ids contain LIKE wildcards, and reads via a custom readFile", () => {
    const { db, projects } = setup(DECIDED, "s1");
    const read: string[] = [];
    const r = runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-04T00:00:00.000Z"), readFile: (p) => (read.push(p), jsonl) });
    expect(read).toHaveLength(1);
    expect(r.labeled).toBe(5);
  });

  it("never labels items of other questions", () => {
    const { db, projects } = setup(DECIDED, "s1");
    upsertEvalItem(db, { id: "x", question: "ask-check", input_json: "{}", source: "decision:z", model_decision: null, created_at: DECIDED });
    runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-04T00:00:00.000Z") });
    expect(labeledItems(db, "ask-check", "outcome")).toEqual([]);
  });

  it("never writes decisions", () => {
    const { db, projects } = setup(DECIDED, "s1");
    const count = () => (db.prepare("SELECT COUNT(*) AS n FROM decisions").get() as { n: number }).n;
    const before = count();
    runSortOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-04T00:00:00.000Z") });
    expect(count()).toBe(before);
  });
});
