import fs from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { openDb, recordDecision, recordDecisionDetails, upsertEvalItem } from "../../judge/src/db.js";
import type { DecisionRow } from "../../judge/src/db.js";
import { GATES, JudgeLiveSchema, judgeLive, readJudgeLive } from "../src/live-judge.js";
import { tmpDir } from "./helpers.js";

function decision(id: string, question: string, over: Partial<DecisionRow> = {}): DecisionRow {
  return {
    id, ts: "2026-10-04T10:00:00.000Z", question, content_class: "brief", provider: "jev", decision: "ready", confidence: 0.9,
    reason_code: "jev", latency_ms: 300, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided", ...over,
  };
}

function seed(db: ReturnType<typeof openDb>, id: string, session: string | null, row: Partial<DecisionRow> & { question: string }, agreement: "agreed" | "overrode" | "undecided" | null = null): void {
  recordDecision(db, decision(id, row.question, row));
  recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement, session_id: session });
}

describe("judgeLive", () => {
  it("is 'none' for a database with no decisions table, and 'unscoped' when decision_details cannot be tied to a session", () => {
    expect(judgeLive(new Database(":memory:"), "s1")).toEqual({ state: "none" });
    const noDetails = openDb(":memory:");
    noDetails.exec("DROP TABLE decision_details");
    expect(judgeLive(noDetails, "s1")).toEqual({ state: "unscoped" });
    const oldDetails = openDb(":memory:");
    oldDetails.exec("DROP TABLE decision_details; CREATE TABLE decision_details (id TEXT PRIMARY KEY, input_json TEXT NOT NULL)");
    expect(judgeLive(oldDetails, "s1")).toEqual({ state: "unscoped" });
  });

  it("is ok with zero activity for a session with no decisions", () => {
    const live = judgeLive(openDb(":memory:"), "s1");
    expect(JudgeLiveSchema.parse(live)).toMatchObject({ state: "ok", calls: 0, latest: null, labelable: true });
  });

  it("summarizes only this session: calls, providers, agreement, latencies, gates and the latest decision", () => {
    const db = openDb(":memory:");
    seed(db, "a", "s1", { question: "brief-scope", decision: "ready", latency_ms: 400, ts: "2026-10-04T10:00:00.000Z" }, "agreed");
    seed(db, "b", "s1", { question: "ask-check", decision: "continue", latency_ms: 350, ts: "2026-10-04T10:01:00.000Z" }, "overrode");
    seed(db, "c", "s1", { question: "ask-check", decision: "ask", provider: "rules", latency_ms: 0, ts: "2026-10-04T10:02:00.000Z" }, "undecided");
    seed(db, "d", "s1", { question: "wake-gate", decision: null, outcome: "escalated", latency_ms: 9000, ts: "2026-10-04T10:03:00.000Z" });
    seed(db, "e", "other", { question: "ask-check", decision: "ask" });
    seed(db, "f", null, { question: "ask-check", decision: "ask" });
    recordDecision(db, decision("g", "ask-check"));
    upsertEvalItem(db, { id: "item-c", question: "ask-check", input_json: "{}", source: "decision:c", model_decision: "ask", created_at: "2026-10-04T10:02:00.000Z" });

    const live = judgeLive(db, "s1");
    expect(JudgeLiveSchema.parse(live)).toEqual({
      state: "ok", calls: 4, byProvider: { jev: 2, rules: 1 }, undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 350, p95LatencyMs: 400,
      gates: {
        "scope-gate": { question: "brief-scope", fired: 1, byDecision: { ready: 1 }, p50LatencyMs: 400 },
        "done-gate": { question: "ask-check", fired: 2, byDecision: { continue: 1, ask: 1 }, p50LatencyMs: 0 },
        "send-gate": { question: "wake-gate", fired: 1, byDecision: {}, p50LatencyMs: 0 },
      },
      labelable: true,
      latest: { id: "c", ts: "2026-10-04T10:02:00.000Z", question: "ask-check", decision: "ask", provider: "rules", confidence: 0.9, itemId: "item-c" },
    });
  });

  it("has no item id until the decision is imported, and is not labelable without eval_items", () => {
    const db = openDb(":memory:");
    seed(db, "a", "s1", { question: "ask-check", decision: "continue" });
    expect(judgeLive(db, "s1")).toMatchObject({ labelable: true, latest: { id: "a", itemId: null } });
    db.exec("DROP TABLE eval_items");
    expect(judgeLive(db, "s1")).toMatchObject({ labelable: false, latest: { id: "a", itemId: null } });
  });

  it("maps each gate to the question it asks", () => {
    expect(GATES).toEqual({ "scope-gate": "brief-scope", "done-gate": "ask-check", "send-gate": "wake-gate" });
  });
});

describe("readJudgeLive", () => {
  it("is 'none' for a missing file, a directory, and a file that is not a database", () => {
    const dir = tmpDir();
    expect(readJudgeLive(path.join(dir, "missing.sqlite"), "s1")).toEqual({ state: "none" });
    const junk = path.join(dir, "junk.sqlite");
    fs.writeFileSync(junk, "this is not sqlite");
    expect(readJudgeLive(junk, "s1")).toEqual({ state: "none" });
    expect(readJudgeLive(dir, "s1")).toEqual({ state: "none" });
  });

  it("reads a real file read-only and leaves it unchanged", () => {
    const file = path.join(tmpDir(), "decisions.sqlite");
    const db = openDb(file);
    seed(db, "a", "s1", { question: "brief-scope" });
    db.close();
    const before = fs.readFileSync(file);
    expect(readJudgeLive(file, "s1")).toMatchObject({ state: "ok", calls: 1 });
    expect(fs.readFileSync(file).equals(before)).toBe(true);
  });
});
