import { describe, expect, it } from "vitest";

import {
  findUnmappedBriefByTeammateName, findUnmappedBriefBySubagentDispatch,
  getBriefByAgentId, getBriefByToolUseId, getDecision, mapToolUseIdToAgentId, openDb,
  recordDecision, recordFailure, recordUndo, saveBrief,
  getDecisionDetails, labelAgreement, labelCounts, labeledItems, nextUnlabeled,
  pruneDecisionDetails, recordDecisionDetails, recordLabel, upsertEvalItem,
} from "../src/db.js";
import { tmpDb } from "./helpers.js";

describe("openDb", () => {
  it("creates every table", () => {
    const db = openDb(":memory:");
    const tables = (db.prepare("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name").all() as Array<{ name: string }>).map((r) => r.name);
    expect(tables).toEqual(["briefs", "decision_details", "decisions", "eval_items", "failures", "labels"]);
  });

  it("sets WAL and busy_timeout on a real file", () => {
    const db = openDb(tmpDb());
    expect(db.pragma("journal_mode", { simple: true })).toBe("wal");
    expect(db.pragma("busy_timeout", { simple: true })).toBe(2000);
  });

  it("records and retrieves a decision, round-tripping chain_position and skipped", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "d1", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
      provider: "claude-cli", decision: "send", confidence: 1, reason_code: "claude-cli", latency_ms: 3,
      input_digest: "abc", undone_at: null, chain_position: 1,
      skipped: [{ provider: "jev", reason: "unavailable" }], outcome: "decided",
    });
    expect(getDecision(db, "d1")).toMatchObject({
      id: "d1", decision: "send", chain_position: 1,
      skipped: [{ provider: "jev", reason: "unavailable" }], outcome: "decided",
    });
  });

  it("round-trips an empty skipped array and chain_position 0 for a pre-rule", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "d1b", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
      provider: "rules", decision: "drop", confidence: 1, reason_code: "pre-rule", latency_ms: 0,
      input_digest: "abc", undone_at: null, chain_position: 0, skipped: [], outcome: "decided",
    });
    expect(getDecision(db, "d1b")).toMatchObject({ chain_position: 0, skipped: [] });
  });

  it("records and retrieves an escalated row with a null decision and no provider attempt", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "esc1", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
      provider: "none", decision: null, confidence: 0, reason_code: "question-disabled", latency_ms: 0,
      input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "escalated",
    });
    expect(getDecision(db, "esc1")).toMatchObject({ decision: null, outcome: "escalated", provider: "none" });
  });

  it("records and retrieves a failed row (real provider failures/timeouts, not a clean escalation)", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "fail1", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
      provider: "none", decision: null, confidence: 0, reason_code: "no-provider-decided", latency_ms: 0,
      input_digest: "x", undone_at: null, chain_position: 2,
      skipped: [{ provider: "jev", reason: "unavailable" }, { provider: "claude-cli", reason: "timeout" }],
      outcome: "failed",
    });
    expect(getDecision(db, "fail1")).toMatchObject({ decision: null, outcome: "failed" });
  });

  it("returns undefined for an unknown id", () => {
    expect(getDecision(openDb(":memory:"), "nope")).toBeUndefined();
  });

  it("records a failure row", () => {
    const db = openDb(":memory:");
    recordFailure(db, { ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", provider: "claude-cli", reason_code: "timeout" });
    expect(db.prepare("SELECT COUNT(*) as n FROM failures").get()).toEqual({ n: 1 });
  });

  it("marks a decision undone", () => {
    const db = openDb(":memory:");
    recordDecision(db, {
      id: "d2", ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
      provider: "rules", decision: "batch", confidence: 0.9, reason_code: "x", latency_ms: 1,
      input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided",
    });
    recordUndo(db, "d2", "2026-09-27T00:01:00.000Z");
    expect(getDecision(db, "d2")?.undone_at).toBe("2026-09-27T00:01:00.000Z");
  });

  it("recordUndo on an unknown id is a no-op", () => {
    const db = openDb(":memory:");
    expect(() => recordUndo(db, "nope", "2026-09-27T00:01:00.000Z")).not.toThrow();
  });

  it("lets two handles on the same file interleave writes without a throw or a lost row (RF-4)", async () => {
    const file = tmpDb();
    const dbA = openDb(file);
    const dbB = openDb(file);
    const writes: Array<Promise<void>> = [];
    for (let i = 0; i < 20; i++) {
      const db = i % 2 === 0 ? dbA : dbB;
      writes.push(
        Promise.resolve().then(() => {
          recordDecision(db, {
            id: `row-${i}`, ts: "2026-09-27T00:00:00.000Z", question: "wake-gate", content_class: "message-meta",
            provider: "rules", decision: "drop", confidence: 1, reason_code: "pre-rule", latency_ms: 0,
            input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided",
          });
        }),
      );
    }
    await expect(Promise.all(writes)).resolves.not.toThrow();
    const count = dbA.prepare("SELECT COUNT(*) as n FROM decisions").get() as { n: number };
    expect(count.n).toBe(20);
    dbA.close();
    dbB.close();
  });
});

describe("brief store", () => {
  it("saves a brief and retrieves it by tool_use_id before any agent_id mapping exists", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "tu1", sessionId: "s1", promptId: "p1", dispatchName: null, subagentType: "lean-coder", goal: "add X", acceptanceCriteria: "tests pass", proofCommand: "npm test", savedAt: "2026-09-27T00:00:00.000Z" });
    expect(getBriefByToolUseId(db, "tu1")).toMatchObject({ goal: "add X", agentId: null });
  });

  it("maps tool_use_id to agent_id, after which the brief is retrievable by agent_id too", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "tu1", sessionId: "s1", promptId: "p1", dispatchName: null, subagentType: "lean-coder", goal: "add X", acceptanceCriteria: "tests pass", proofCommand: "npm test", savedAt: "2026-09-27T00:00:00.000Z" });
    mapToolUseIdToAgentId(db, "tu1", "agent-42");
    expect(getBriefByAgentId(db, "agent-42")).toMatchObject({ goal: "add X" });
  });

  it("returns undefined for an unknown tool_use_id or agent_id", () => {
    const db = openDb(":memory:");
    expect(getBriefByToolUseId(db, "nope")).toBeUndefined();
    expect(getBriefByAgentId(db, "nope")).toBeUndefined();
  });

  it("mapping an unknown tool_use_id is a no-op, not a throw", () => {
    const db = openDb(":memory:");
    expect(() => mapToolUseIdToAgentId(db, "nope", "agent-1")).not.toThrow();
  });

  it("finds the oldest unmapped brief by teammate name (exact match, Probe gate item 1 case 1)", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "tu1", sessionId: "s1", promptId: "p1", dispatchName: "builder-a", subagentType: "general-purpose", goal: "first", acceptanceCriteria: "x", proofCommand: "x", savedAt: "2026-09-27T00:00:00.000Z" });
    saveBrief(db, { toolUseId: "tu2", sessionId: "s1", promptId: "p2", dispatchName: "builder-a", subagentType: "general-purpose", goal: "second", acceptanceCriteria: "x", proofCommand: "x", savedAt: "2026-09-27T00:01:00.000Z" });
    expect(findUnmappedBriefByTeammateName(db, "builder-a")).toMatchObject({ toolUseId: "tu1", goal: "first" });
  });

  it("finds the oldest unmapped brief by (session, prompt, subagentType) for an unnamed dispatch (Probe gate item 1 case 2)", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "tu3", sessionId: "s1", promptId: "p1", dispatchName: null, subagentType: "lean-coder", goal: "x", acceptanceCriteria: "x", proofCommand: "x", savedAt: "2026-09-27T00:00:00.000Z" });
    expect(findUnmappedBriefBySubagentDispatch(db, "s1", "p1", "lean-coder")).toMatchObject({ toolUseId: "tu3" });
    expect(findUnmappedBriefBySubagentDispatch(db, "s1", "p1", "lean-reviewer")).toBeUndefined();
  });

  it("skips an already-mapped brief when looking up either unmapped-brief finder", () => {
    const db = openDb(":memory:");
    saveBrief(db, { toolUseId: "tu4", sessionId: "s1", promptId: "p1", dispatchName: "builder-a", subagentType: "general-purpose", goal: "x", acceptanceCriteria: "x", proofCommand: "x", savedAt: "2026-09-27T00:00:00.000Z" });
    mapToolUseIdToAgentId(db, "tu4", "agent-1");
    expect(findUnmappedBriefByTeammateName(db, "builder-a")).toBeUndefined();
  });
});

describe("decision_details + eval tables", () => {
  const decision = (id: string, ts: string) => ({
    id, ts, question: "wake-gate", content_class: "message-meta", provider: "jev" as const, decision: "send", confidence: 0.9,
    reason_code: "jev", latency_ms: 300, input_digest: "d", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const,
  });

  it("creates the new tables on an existing db without touching decisions rows (RF-2)", () => {
    const file = tmpDb();
    const first = openDb(file);
    recordDecision(first, decision("a", "2026-10-01T00:00:00.000Z"));
    first.close();
    const reopened = openDb(file);
    expect(getDecision(reopened, "a")?.decision).toBe("send");
    recordDecisionDetails(reopened, { id: "a", input_json: "{}", probabilities: { send: 0.9, batch: 0.1 }, rules_opinion: null, agreement: null });
    expect(getDecisionDetails(reopened, "a")).toEqual({ id: "a", input_json: "{}", probabilities: { send: 0.9, batch: 0.1 }, rules_opinion: null, agreement: null, session_id: null });
  });

  it("returns undefined for missing details and round-trips null probabilities and a session id", () => {
    const db = openDb(":memory:");
    expect(getDecisionDetails(db, "nope")).toBeUndefined();
    recordDecisionDetails(db, { id: "x", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "agreed", session_id: "s1" });
    expect(getDecisionDetails(db, "x")).toEqual({ id: "x", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "agreed", session_id: "s1" });
  });

  it("prunes details of decisions older than the cutoff", () => {
    const db = openDb(":memory:");
    recordDecision(db, decision("old", "2026-08-01T00:00:00.000Z"));
    recordDecision(db, decision("new", "2026-10-01T00:00:00.000Z"));
    for (const id of ["old", "new"]) recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement: null });
    expect(pruneDecisionDetails(db, "2026-09-01T00:00:00.000Z")).toBe(1);
    expect(getDecisionDetails(db, "old")).toBeUndefined();
    expect(getDecisionDetails(db, "new")).toBeDefined();
  });

  it("upserts eval items once per source, serves the oldest unlabeled, and lists labeled ones without skips", () => {
    const db = openDb(":memory:");
    const item = (id: string, source: string, created_at: string) => ({ id, question: "wake-gate", input_json: "{}", source, model_decision: "send", created_at });
    expect(upsertEvalItem(db, item("i1", "decision:a", "2026-10-01T00:00:00.000Z"))).toBe(true);
    expect(upsertEvalItem(db, item("i1b", "decision:a", "2026-10-02T00:00:00.000Z"))).toBe(false);
    upsertEvalItem(db, item("i2", "decision:b", "2026-10-02T00:00:00.000Z"));
    upsertEvalItem(db, item("i3", "decision:c", "2026-10-03T00:00:00.000Z"));
    expect(nextUnlabeled(db, "wake-gate")?.id).toBe("i1");
    recordLabel(db, "i1", "batch", "2026-10-04T00:00:00.000Z");
    recordLabel(db, "i2", "skip", "2026-10-04T00:00:00.000Z");
    expect(nextUnlabeled(db)?.id).toBe("i3");
    expect(nextUnlabeled(db, "other")).toBeUndefined();
    expect(labeledItems(db, "wake-gate").map((r) => [r.id, r.label])).toEqual([["i1", "batch"]]);
    expect(labelCounts(db)).toEqual([{ question: "wake-gate", items: 3, labeled: 1, skipped: 1 }]);
  });

  it("keeps one label per source, applies override > outcome > adjudicator precedence, and measures agreement", () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, { id, question: "ask-check", input_json: "{}", source: `decision:${id}`, model_decision: "continue", created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "a", "ask", "t", "adjudicator");
    recordLabel(db, "a", "continue", "t", "outcome");
    recordLabel(db, "b", "ask", "t", "adjudicator");
    recordLabel(db, "b", "ask", "t", "outcome");
    expect(labeledItems(db, "ask-check").map((r) => [r.id, r.label, r.label_source])).toEqual([["a", "continue", "outcome"], ["b", "ask", "outcome"]]);
    expect(labeledItems(db, "ask-check", "adjudicator").map((r) => r.label)).toEqual(["ask", "ask"]);
    recordLabel(db, "a", "ask", "t", "override");
    expect(labeledItems(db, "ask-check")[0]).toMatchObject({ label: "ask", label_source: "override" });
    expect(labelAgreement(db, "ask-check")).toEqual({ shared: 2, agreed: 1 });
    expect(nextUnlabeled(db, "ask-check", "adjudicator")).toBeUndefined();
  });

  it("reports zero agreement when no item has both outcome and adjudicator labels", () => {
    const db = openDb(":memory:");
    expect(labelAgreement(db, "ask-check")).toEqual({ shared: 0, agreed: 0 });
  });

  it("nextUnlabeled with a source ignores labels from other sources", () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "a", question: "q", input_json: "{}", source: "s", model_decision: null, created_at: "t" });
    recordLabel(db, "a", "x", "t", "outcome");
    expect(nextUnlabeled(db, "q", "adjudicator")?.id).toBe("a");
    expect(nextUnlabeled(db, "q", "outcome")).toBeUndefined();
  });

  it("nextUnlabeled can exclude item ids", () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, { id, question: "q", input_json: "{}", source: `decision:${id}`, model_decision: null, created_at: `2026-10-01T00:00:0${id === "a" ? 0 : 1}.000Z` });
    expect(nextUnlabeled(db, "q", "adjudicator", new Set(["a"]))?.id).toBe("b");
    expect(nextUnlabeled(db, "q", "adjudicator", new Set())?.id).toBe("a");
    expect(nextUnlabeled(db, "q", "adjudicator", new Set(["a", "b"]))).toBeUndefined();
  });
});
