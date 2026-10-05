import fs from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { openDb as openJudgeDb, recordDecision, recordDecisionDetails } from "../../judge/src/db.js";
import { parseArgs } from "../src/args.js";
import type { CliOptions } from "../src/args.js";
import { findSessionFiles, pruneLiveDbs, renderLive, runLive } from "../src/live.js";
import type { LiveSnapshot } from "../src/live-snapshot.js";
import { LiveSnapshotSchema } from "../src/live-snapshot.js";
import { projectFromCwd } from "../src/transcript/source.js";
import { appendRaw, assistant, tmpDir, user, writeLines } from "./helpers.js";

const NOW = new Date("2026-10-04T12:00:00.000Z");
const CWD = "/work/acme.app";

interface World { root: string; options: CliOptions; transcript: string; state: string }

function world(extra: string[] = [], lines?: string[]): World {
  const root = tmpDir();
  const transcript = path.join(root, "projects", projectFromCwd(CWD), "s1.jsonl");
  if (lines !== undefined) writeLines(transcript, lines);
  const state = path.join(root, "state");
  const parsed = parseArgs(["live", "--session", "s1", "--cwd", CWD, "--window", "200000", "--projects-dir", path.join(root, "projects"), "--state-dir", state, ...extra], NOW, "/home/x");
  if (!parsed.ok) throw new Error(parsed.error);
  return { root, options: parsed.options, transcript, state };
}

const calls = (): string[] => [
  user("hello", { ts: "2026-10-04T10:00:00.000Z" }),
  assistant({ id: "m1", ts: "2026-10-04T10:00:01.000Z", input: 100, cacheRead: 40_000, output: 10 }),
  assistant({ id: "m2", ts: "2026-10-04T10:01:00.000Z", input: 100, cacheRead: 84_000, output: 20 }),
];

describe("runLive", () => {
  it("reads this session's transcript into a snapshot that satisfies the schema", () => {
    const w = world([], calls());
    const snap = runLive(w.options, () => NOW.getTime());
    expect(LiveSnapshotSchema.parse(snap)).toEqual(snap);
    expect(snap).toMatchObject({
      v: 1, sessionId: "s1", at: "2026-10-04T12:00:00.000Z",
      transcript: { found: true, files: 1 },
      context: { tokens: 84_100, window: 200_000, percent: 42 },
      usage: { calls: 2, subagentCalls: 0, contextTokens: 40_100 + 84_100, outputTokens: 30, callsOver200k: 0 },
      judge: { state: "none" }, contextGuard: { fires: 0 }, wakes: { queuedNow: 0 },
    });
    expect(snap.ingest.newLines).toBeGreaterThan(0);
  });

  it("is incremental: an unchanged transcript ingests nothing, an appended line ingests only itself", () => {
    const w = world([], calls());
    const first = runLive(w.options);
    const again = runLive(w.options);
    expect(again.ingest.newLines).toBe(0);
    expect(again.usage).toEqual(first.usage);
    appendRaw(w.transcript, `${assistant({ id: "m3", ts: "2026-10-04T10:02:00.000Z", input: 10, cacheRead: 90_000, output: 5 })}\n`);
    const grown = runLive(w.options);
    expect(grown.ingest.newLines).toBe(1);
    expect(grown.usage.calls).toBe(3);
    expect(grown.context.tokens).toBe(90_010);
    expect(fs.existsSync(path.join(w.state, "scorer", "live", "s1.sqlite"))).toBe(true);
  });

  it("counts a half-written last line only once it is complete (the offset stops at the last newline)", () => {
    const w = world([], calls());
    runLive(w.options);
    const line = assistant({ id: "m3", ts: "2026-10-04T10:02:00.000Z", input: 10, cacheRead: 90_000, output: 5 });
    appendRaw(w.transcript, line.slice(0, 40));
    expect(runLive(w.options).usage.calls).toBe(2);
    appendRaw(w.transcript, `${line.slice(40)}\n`);
    const done = runLive(w.options);
    expect(done.usage.calls).toBe(3);
    expect(done.ingest.newLines).toBe(1);
  });

  it("never touches scorer.sqlite, the daily report's database", () => {
    const w = world([], calls());
    runLive(w.options);
    expect(fs.existsSync(path.join(w.state, "scorer", "scorer.sqlite"))).toBe(false);
  });

  it("adds this session's judge activity, context-guard fires and queued wakes, and leaves the judge db unchanged", () => {
    const w = world([], calls());
    const judgeFile = path.join(w.state, "judge", "decisions.sqlite");
    fs.mkdirSync(path.dirname(judgeFile), { recursive: true });
    const db = openJudgeDb(judgeFile);
    recordDecision(db, { id: "d1", ts: "2026-10-04T10:00:30.000Z", question: "ask-check", content_class: "brief", provider: "jev", decision: "continue", confidence: 0.9, reason_code: "jev", latency_ms: 350, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    recordDecisionDetails(db, { id: "d1", input_json: "{}", probabilities: null, rules_opinion: "continue", agreement: "agreed", session_id: "s1" });
    db.pragma("wal_checkpoint(TRUNCATE)");
    db.close();
    const guard = path.join(w.state, "context-guard");
    fs.mkdirSync(guard, { recursive: true });
    fs.writeFileSync(path.join(guard, "fires.jsonl"), [
      JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }),
      JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentId: null, tokens: 210_000, sessionId: "other" }),
    ].join("\n"));
    const outbox = path.join(w.state, "judge", "outbox");
    fs.mkdirSync(outbox, { recursive: true });
    fs.writeFileSync(path.join(outbox, "s1.jsonl"), `${JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentType: null, text: "step done" })}\n`);
    const before = fs.readFileSync(judgeFile);

    const snap = runLive(w.options);
    expect(snap.judge).toMatchObject({ state: "ok", calls: 1, agreed: 1, byProvider: { jev: 1 }, latest: { id: "d1", decision: "continue" } });
    expect(snap.contextGuard.fires).toBe(1);
    expect(snap.wakes.queuedNow).toBe(1);
    expect(fs.readFileSync(judgeFile).equals(before)).toBe(true);
  });

  it("reports no transcript without creating a database file when the session has no files", () => {
    const w = world();
    const snap = runLive(w.options);
    expect(snap).toMatchObject({ transcript: { found: false, files: 0 }, context: { tokens: null, percent: null }, usage: { calls: 0 } });
    expect(fs.existsSync(path.join(w.state, "scorer", "live"))).toBe(false);
  });

  it("reports unreadable transcripts as readErrors instead of silently stale numbers", () => {
    const w = world([], calls());
    expect(runLive(w.options).ingest.readErrors).toBe(0);
    fs.rmSync(w.transcript);
    fs.mkdirSync(w.transcript);
    // a directory where the transcript should be: found, but unreadable
    expect(runLive(w.options).ingest.readErrors).toBe(1);
  });

  it("finds the transcript without a cwd, and when the cwd names the wrong project", () => {
    const w = world([], calls());
    const noCwd = { ...w.options, liveCwd: null };
    expect(runLive(noCwd).transcript.found).toBe(true);
    expect(runLive({ ...w.options, liveCwd: "/somewhere/else" }).transcript.found).toBe(true);
  });
});

describe("findSessionFiles", () => {
  it("prefers the hinted project and falls back to a scan", () => {
    const w = world([], calls());
    const projects = path.join(w.root, "projects");
    expect(findSessionFiles(projects, "s1", CWD)).toHaveLength(1);
    expect(findSessionFiles(projects, "s1", null)).toHaveLength(1);
    expect(findSessionFiles(projects, "s1", "/other")).toHaveLength(1);
    expect(findSessionFiles(projects, "nope", CWD)).toEqual([]);
  });
});

describe("pruneLiveDbs", () => {
  const DAY = 86_400_000;

  function fill(n: number, ageDays: number): string {
    const dir = tmpDir();
    for (let i = 0; i < n; i += 1) {
      const file = path.join(dir, `old-${i}.sqlite`);
      fs.writeFileSync(file, "");
      const t = new Date(NOW.getTime() - ageDays * DAY);
      fs.utimesSync(file, t, t);
    }
    return dir;
  }

  it("does nothing while the directory is small", () => {
    const dir = fill(30, 20);
    expect(pruneLiveDbs(dir, "s1", NOW.getTime())).toBe(0);
    expect(fs.readdirSync(dir)).toHaveLength(30);
  });

  it("removes sessions untouched for a week, keeps fresh ones and the current session's files, and survives a dangling link", () => {
    const dir = fill(31, 10);
    const fresh = path.join(dir, "fresh.sqlite");
    fs.writeFileSync(fresh, "");
    fs.writeFileSync(path.join(dir, "s1.sqlite"), "");
    fs.writeFileSync(path.join(dir, "s1.sqlite-wal"), "");
    for (const keep of ["s1.sqlite", "s1.sqlite-wal"]) {
      const t = new Date(NOW.getTime() - 30 * DAY);
      fs.utimesSync(path.join(dir, keep), t, t);
    }
    fs.symlinkSync(path.join(dir, "missing-target"), path.join(dir, "dangling.sqlite"));
    expect(pruneLiveDbs(dir, "s1", Date.now())).toBe(31);
    expect(fs.readdirSync(dir).sort()).toEqual(["dangling.sqlite", "fresh.sqlite", "s1.sqlite", "s1.sqlite-wal"]);
  });
});

describe("renderLive", () => {
  const base: LiveSnapshot = {
    v: 1, sessionId: "s1", at: "2026-10-04T12:00:00.000Z", transcript: { found: true, files: 1 }, ingest: { newLines: 0, ms: 5, readErrors: 0 },
    context: { tokens: 84_100, window: 200_000, percent: 42 },
    usage: { calls: 31, subagentCalls: 9, contextTokens: 1_900_000, outputTokens: 52_000, callsOver200k: 2 },
    judge: { state: "none" }, contextGuard: { fires: 1 }, wakes: { queuedNow: 2 },
  };
  const ok = {
    state: "ok" as const, calls: 4, byProvider: { jev: 3, rules: 1 }, undecided: 1, agreed: 2, overrode: 1, p50LatencyMs: 350, p95LatencyMs: 900,
    gates: {
      "scope-gate": { question: "brief-scope", fired: 1, byDecision: {}, p50LatencyMs: 0 },
      "done-gate": { question: "ask-check", fired: 2, byDecision: {}, p50LatencyMs: 0 },
      "send-gate": { question: "wake-gate", fired: 0, byDecision: {}, p50LatencyMs: 0 },
    },
    labelable: false, latest: null,
  };

  it("prints context, session, judge, gates and wakes", () => {
    expect(renderLive({ ...base, judge: ok })).toBe([
      "context   84.1k of 200.0k (42%)",
      "session   31 calls (9 subagent) · 1.9M in · 52.0k out · 2 over 200k",
      "judge     4 calls · jev 75.0% · rules 25.0% · agreed 2 · overrode 1 · unsure 1 · p50 350ms",
      "gates     scope-gate 1 · done-gate 2 · send-gate 0",
      "wakes     2 queued · context-guard 1 fires",
      "",
    ].join("\n"));
  });

  it("warns when transcripts could not be read", () => {
    expect(renderLive({ ...base, ingest: { ...base.ingest, readErrors: 2 } })).toContain("warning   2 transcript read errors");
  });

  it("says so when there is no transcript, an unknown context, no judge activity, or an unlinked judge", () => {
    expect(renderLive({ ...base, transcript: { found: false, files: 0 } })).toContain("context   no transcript found for this session");
    expect(renderLive({ ...base, context: { tokens: null, window: 200_000, percent: null } })).toContain("context   unknown");
    expect(renderLive({ ...base, judge: { state: "unscoped" } })).toContain("judge     not linked to sessions yet");
    expect(renderLive({ ...base, judge: { ...ok, calls: 0, byProvider: {} } })).toContain("judge     0 calls · agreed 2");
  });
});
