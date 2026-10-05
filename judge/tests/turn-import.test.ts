import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { labeledItems, nextUnlabeled, openDb, saveBrief } from "../src/db.js";
import { importTurns, parseTurns, runImportTurns, survival, turnDiff, turnOutcome } from "../src/turn-import.js";

const jsonl = fs.readFileSync(path.join(import.meta.dirname, "fixtures", "turns.jsonl"), "utf8");
const now = () => new Date("2026-10-04T00:00:00.000Z");
type E = { file: string; before: string; after: string };
const t = (index: number, prompt: string, edits: E[]) => ({ sessionId: "s", index, prompt, edits });
const row = (o: object) => JSON.stringify(o);
const user = (sessionId: string, content: unknown, timestamp?: string) => row({ type: "user", sessionId, timestamp, message: { content } });
const edit = (sessionId: string, name: string, input: object) => row({ type: "assistant", sessionId, message: { content: [{ type: "tool_use", name, input }] } });

describe("parseTurns", () => {
  it("splits on real user prompts only and collects edits per turn (RF-3, machine text excluded)", () => {
    const turns = parseTurns(jsonl);
    expect(turns.map((x) => [x.index, x.prompt, x.edits.length])).toEqual([[0, "Make save idempotent", 1], [1, "what does pending mean?", 0]]);
  });

  it("ignores blank and unparseable lines", () => {
    expect(parseTurns(`\nnot json\n${jsonl}`)).toHaveLength(2);
  });

  it("reads MultiEdit and Write, ignores other tools, pre-prompt edits and non-text rows", () => {
    const lines = [
      edit("s", "Write", { file_path: "/w.ts", content: "ignored: no turn yet" }),
      user("s", "go"),
      edit("s", "MultiEdit", { file_path: "/m.ts", edits: [{ old_string: "a", new_string: "b" }, {}] }),
      edit("s", "MultiEdit", { file_path: "/m.ts" }),
      edit("s", "Write", { file_path: "/w.ts", content: "hi" }),
      edit("s", "Bash", { command: "ls" }),
      edit("s", "Edit", {}),
      row({ type: "assistant", sessionId: "s", message: { content: [{ type: "tool_use", name: "Edit" }] } }),
      row({ type: "assistant", sessionId: "s", message: { content: "plain string" } }),
      row({ type: "user", message: { content: "no session id" } }),
    ].join("\n");
    const turns = parseTurns(lines);
    expect(turns[0].edits).toEqual([
      { file: "/m.ts", before: "a", after: "b" }, { file: "/m.ts", before: "", after: "" },
      { file: "/w.ts", before: "", after: "hi" }, { file: "", before: "", after: "" }, { file: "", before: "", after: "" },
    ]);
    expect(turns[1]).toMatchObject({ sessionId: "unknown", index: 0 });
  });

  it("excludes compaction summaries and keeps per-session indexes", () => {
    const turns = parseTurns([user("a", "one"), user("b", "two"), user("a", "This session is being continued from a previous conversation ..."), user("a", "three")].join("\n"));
    expect(turns.map((x) => [x.sessionId, x.index, x.prompt])).toEqual([["a", 0, "one"], ["b", 0, "two"], ["a", 1, "three"]]);
  });
});

describe("turnDiff", () => {
  it("renders before/after per file; Write has an empty before", () => {
    expect(turnDiff([{ file: "/r/a.ts", before: "x", after: "y\nz" }, { file: "/r/b.ts", before: "", after: "new" }]))
      .toBe("--- /r/a.ts\n- x\n+ y\n+ z\n--- /r/b.ts\n+ new");
  });
});

describe("importTurns", () => {
  it("imports one item per turn with edits, once, and uses the session brief when present", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "task:s1", sessionId: "s1", promptId: "p", dispatchName: null, subagentType: "main", goal: "Idempotent saves", acceptanceCriteria: "double click saves once", proofCommand: "npm test", savedAt: "2026-10-01T00:00:00.000Z" });
    expect(importTurns(db, jsonl, now)).toEqual({ imported: 1, skipped: 1 });
    expect(importTurns(db, jsonl, now)).toEqual({ imported: 0, skipped: 2 });
    const item = nextUnlabeled(db, "turn-progress", "adjudicator");
    expect(item?.source).toBe("transcript:s1:0");
    expect(item?.model_decision).toBeNull();
    expect(JSON.parse(item?.input_json ?? "{}")).toEqual({
      problem: "Goal: Idempotent saves\n\nMake save idempotent", acceptanceCriteria: "double click saves once",
      turnDiff: "--- /r/save.ts\n- save()\n+ if (pending) return;\n+ save()", priorDiffStat: "0 files edited earlier in session", signals: "",
    });
    expect(labeledItems(db, "turn-progress", "outcome").map((r) => r.label)).toEqual(["progressing"]);
    expect(db.prepare("SELECT COUNT(*) AS n FROM decisions").get()).toEqual({ n: 0 });
  });

  it("redacts secrets and keeps stored input valid JSON when capped", () => {
    const db = openDb(":memory:");
    const big = `token sk-${"a".repeat(30)}\n${"x".repeat(40000)}`;
    importTurns(db, [user("s", "go"), edit("s", "Write", { file_path: "/a.ts", content: big })].join("\n"), now);
    const item = nextUnlabeled(db, "turn-progress", "adjudicator");
    const parsed = JSON.parse(item?.input_json ?? "") as { turnDiff: string };
    expect(parsed.turnDiff).toContain("[REDACTED]");
    expect(parsed.turnDiff).toContain("[truncated");
    expect(item?.input_json).not.toContain("sk-aaaa");
  });

  it("counts files edited earlier in the session", () => {
    const db = openDb(":memory:");
    const lines = [user("s", "a"), edit("s", "Write", { file_path: "/1.ts", content: "x" }), user("s", "b"), edit("s", "Write", { file_path: "/2.ts", content: "y" }), user("s", "c"), edit("s", "Write", { file_path: "/1.ts", content: "z" })].join("\n");
    importTurns(db, lines, now);
    const stats = db.prepare("SELECT input_json FROM eval_items ORDER BY source").all().map((r) => (JSON.parse((r as { input_json: string }).input_json) as { priorDiffStat: string }).priorDiffStat);
    expect(stats).toEqual(["0 files edited earlier in session", "1 files edited earlier in session", "2 files edited earlier in session"]);
  });

  it("labels a session's last turn only once the session ended 6h+ before import", () => {
    const lines = (end: string) => [user("s", "go", "2026-10-03T10:00:00.000Z"), edit("s", "Write", { file_path: "/a.ts", content: "x" }), row({ type: "assistant", sessionId: "s", timestamp: end, message: { content: [] } })].join("\n");
    const open = openDb(":memory:");
    importTurns(open, lines("2026-10-03T20:00:00.000Z"), now);
    expect(labeledItems(open, "turn-progress", "outcome")).toEqual([]);
    const closed = openDb(":memory:");
    importTurns(closed, lines("2026-10-03T17:59:59.000Z"), now);
    expect(labeledItems(closed, "turn-progress", "outcome").map((r) => r.label)).toEqual(["progressing"]);
    const noTimes = openDb(":memory:");
    importTurns(noTimes, [user("s", "go"), edit("s", "Write", { file_path: "/a.ts", content: "x" })].join("\n"), now);
    expect(labeledItems(noTimes, "turn-progress", "outcome")).toEqual([]);
  });
});

describe("survival / turnOutcome", () => {
  it("counts an added line as dead once a later edit removes it", () => {
    const turns = [t(0, "add guard", [{ file: "a.ts", before: "save()", after: "if (p) return;\nsave()" }]), t(1, "now docs", [{ file: "a.ts", before: "if (p) return;", after: "" }])];
    expect(survival(turns, 0)).toBe(0.5);
    expect(survival(turns, 1)).toBeNull();
  });

  it("kills lines on a later Write without them, spares other files and kept lines", () => {
    const turns = [
      t(0, "a", [{ file: "a.ts", before: "", after: "x\ny" }, { file: "b.ts", before: "", after: "q" }]),
      t(1, "b", [{ file: "a.ts", before: "", after: "y\nnew" }, { file: "b.ts", before: "zzz", after: "other" }]),
    ];
    expect(survival(turns, 0)).toBeCloseTo(2 / 3);
    expect(survival(turns, 5)).toBeNull();
  });

  it("an edit that keeps the line in its new_string does not kill it", () => {
    const turns = [t(0, "a", [{ file: "a.ts", before: "", after: "x" }]), t(1, "b", [{ file: "a.ts", before: "x", after: "x\ny" }])];
    expect(survival(turns, 0)).toBe(1);
  });

  it("ignores blank and brace-only added lines", () => {
    expect(survival([t(0, "a", [{ file: "a.ts", before: "", after: "{\n\n  }\n};\nreal" }])], 0)).toBe(1);
    expect(survival([t(0, "a", [{ file: "a.ts", before: "", after: "{\n\n}" }])], 0)).toBeNull();
    const dies = [t(0, "a", [{ file: "a.ts", before: "", after: "{\nreal" }]), t(1, "b", [{ file: "a.ts", before: "real", after: "" }])];
    expect(survival(dies, 0)).toBe(0);
  });

  it("labels kept work progressing and reverted or corrected work stalled", () => {
    const kept = [t(0, "add guard", [{ file: "a.ts", before: "", after: "x\ny" }]), t(1, "now docs", [])];
    const corrected = [t(0, "add guard", [{ file: "a.ts", before: "", after: "x" }]), t(1, "no, wrong file", [])];
    const interrupted = [t(0, "add guard", [{ file: "a.ts", before: "", after: "x" }]), t(1, "[Request interrupted by user]", [])];
    const reverted = [t(0, "add guard", [{ file: "a.ts", before: "", after: "x" }]), t(1, "go on", [{ file: "a.ts", before: "x", after: "" }])];
    expect(turnOutcome(kept, 0)).toBe("progressing");
    expect(turnOutcome(corrected, 0)).toBe("stalled");
    expect(turnOutcome(interrupted, 0)).toBe("stalled");
    expect(turnOutcome(reverted, 0)).toBe("stalled");
    expect(turnOutcome(kept, 1)).toBeNull();
  });

  it("gives no label for middling survival, and none for the last turn unless the session is closed", () => {
    const middling = [t(0, "a", [{ file: "a.ts", before: "", after: "x\ny" }]), t(1, "b", [{ file: "a.ts", before: "x", after: "" }])];
    expect(turnOutcome(middling, 0)).toBeNull();
    const last = [t(0, "a", [{ file: "a.ts", before: "", after: "x" }])];
    expect(turnOutcome(last, 0)).toBeNull();
    expect(turnOutcome(last, 0, true)).toBe("progressing");
  });
});

describe("runImportTurns", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "turns-"));
  const fresh = path.join(dir, "fresh.jsonl");
  const old = path.join(dir, "old.jsonl");
  fs.writeFileSync(fresh, jsonl);
  fs.writeFileSync(old, jsonl.replaceAll('"s1"', '"s2"'));
  const long = new Date("2026-09-01T00:00:00.000Z");
  fs.utimesSync(old, long, long);
  fs.utimesSync(fresh, now(), now());

  it("sums over files and filters by mtime with --since", () => {
    expect(runImportTurns(openDb(":memory:"), [fresh, old], now)).toEqual({ exitCode: 0, stdout: '{"imported":2,"skipped":2}' });
    expect(runImportTurns(openDb(":memory:"), [fresh, old, "--since", "14d"], now)).toEqual({ exitCode: 0, stdout: '{"imported":1,"skipped":1}' });
  });

  it("rejects bad usage and unreadable files", () => {
    const db = openDb(":memory:");
    expect(runImportTurns(db, [], now).exitCode).toBe(1);
    expect(runImportTurns(db, [fresh, "--since", "soon"], now).stderr).toContain("--since");
    expect(runImportTurns(db, [fresh, "--since"], now).exitCode).toBe(1);
    expect(runImportTurns(db, [path.join(dir, "missing.jsonl")], now).stderr).toContain("cannot read");
  });
});
