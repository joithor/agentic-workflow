import { describe, expect, it } from "vitest";

import { labeledItems, openDb, recordDecision, recordDecisionDetails, recordLabel, upsertEvalItem, type DecisionRow } from "../src/db.js";
import { runLabelImport, runLabelSet, runLabelStatus } from "../src/label.js";

const now = () => new Date("2026-10-04T00:00:00.000Z");
const WAKE = '{"text":"step 2 done","senderKind":"teammate"}';

function dec(id: string, over: Partial<DecisionRow> = {}): DecisionRow {
  return { id, ts: "2026-10-03T00:00:00.000Z", question: "wake-gate", content_class: "message-meta", provider: "claude-cli", decision: "batch", confidence: 0.9, reason_code: "x", latency_ms: 1, input_digest: "d", undone_at: null, chain_position: 0, skipped: [], outcome: "decided", ...over };
}
function seed(db: ReturnType<typeof openDb>, row: DecisionRow, input = WAKE, withDetails = true): void {
  recordDecision(db, row);
  if (withDetails) recordDecisionDetails(db, { id: row.id, input_json: input, probabilities: null, rules_opinion: null, agreement: null, session_id: null });
}

describe("runLabelImport", () => {
  it("imports decided rows with details once; skips escalated, old, detail-less, unknown-question, unparsable and schema-invalid rows", () => {
    const db = openDb(":memory:");
    seed(db, dec("ok"));
    seed(db, dec("esc", { outcome: "escalated", decision: null }));
    seed(db, dec("old", { ts: "2026-09-01T00:00:00.000Z" }));
    seed(db, dec("nodet"), WAKE, false);
    seed(db, dec("unk", { question: "prompt-sort" }));
    seed(db, dec("trunc"), '{"text":"cut off');
    seed(db, dec("bad"), '{"nope":1}');
    const opts = { sinceIso: "2026-10-01T00:00:00.000Z" };
    expect(runLabelImport(db, opts, now)).toEqual({ exitCode: 0, stdout: '{"imported":1,"skipped_unparsable":1}' });
    expect(runLabelImport(db, opts, now).stdout).toBe('{"imported":0,"skipped_unparsable":1}');
    const row = db.prepare("SELECT * FROM eval_items").all() as Array<{ source: string; model_decision: string }>;
    expect(row).toHaveLength(1);
    expect(row[0]).toMatchObject({ source: "decision:ok", model_decision: "batch" });
  });

  it("counts inputs that are not valid JSON (e.g. truncated at the cap) as skipped_unparsable", () => {
    const db = openDb(":memory:");
    seed(db, dec("trunc"), '{"text":"cut off');
    seed(db, dec("bad"), '{"nope":1}');
    seed(db, dec("ok"));
    expect(runLabelImport(db, { sinceIso: "2026-10-01T00:00:00.000Z" }, now).stdout).toBe('{"imported":1,"skipped_unparsable":1}');
  });

  it("filters by question", () => {
    const db = openDb(":memory:");
    seed(db, dec("w"));
    seed(db, dec("a", { question: "ask-check", decision: "ask" }), '{"transcriptTail":"x"}');
    expect(runLabelImport(db, { sinceIso: "2026-10-01T00:00:00.000Z", question: "wake-gate" }, now).stdout).toBe('{"imported":1,"skipped_unparsable":0}');
    expect(runLabelImport(db, { sinceIso: "2026-10-01T00:00:00.000Z" }, now).stdout).toBe('{"imported":1,"skipped_unparsable":0}');
  });
});

describe("runLabelSet", () => {
  it("records an override", () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "i1", question: "wake-gate", input_json: WAKE, source: "decision:a", model_decision: "send", created_at: "2026-10-01T00:00:00.000Z" });
    expect(runLabelSet(db, "i1", "batch", now).exitCode).toBe(0);
    expect(labeledItems(db, "wake-gate", "override").map((r) => r.label)).toEqual(["batch"]);
    expect(runLabelSet(db, "i1", "skip", now).exitCode).toBe(0);
  });

  it("rejects an off-enum label listing the valid options, and an unknown item", () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "i1", question: "wake-gate", input_json: WAKE, source: "decision:a", model_decision: "send", created_at: "2026-10-01T00:00:00.000Z" });
    expect(runLabelSet(db, "i1", "maybe", now)).toEqual({ exitCode: 1, stdout: "", stderr: "label must be one of: send, batch, drop, skip" });
    expect(labeledItems(db, "wake-gate")).toEqual([]);
    expect(runLabelSet(db, "ghost", "send", now)).toEqual({ exitCode: 1, stdout: "", stderr: "unknown item: ghost" });
  });

  it("only accepts skip for an item whose question is not registered", () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "p1", question: "prompt-sort", input_json: "{}", source: "decision:p", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    expect(runLabelSet(db, "p1", "send", now).stderr).toBe("label must be one of: skip");
    expect(runLabelSet(db, "p1", "skip", now).exitCode).toBe(0);
  });
});

describe("runLabelStatus", () => {
  it("returns counts plus outcome/adjudicator agreement per question", () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, { id, question: "ask-check", input_json: "{}", source: `decision:${id}`, model_decision: "ask", created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "a", "ask", "t", "outcome");
    recordLabel(db, "a", "ask", "t", "adjudicator");
    recordLabel(db, "b", "ask", "t", "outcome");
    recordLabel(db, "b", "continue", "t", "adjudicator");
    const r = runLabelStatus(db);
    expect(r.exitCode).toBe(0);
    expect(JSON.parse(r.stdout)).toEqual([{ question: "ask-check", items: 2, labeled: 2, skipped: 0, agreement: { shared: 2, agreed: 1 } }]);
  });
});
