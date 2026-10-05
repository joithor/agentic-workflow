import { describe, expect, it } from "vitest";

import { labeledItems, openDb, upsertEvalItem } from "../../src/db.js";
import { ALL_AXES, axisOutputs, sortQuestionName } from "../../src/prompt-sort/axes.js";
import { adjudicateSort } from "../../src/prompt-sort/adjudicate.js";
import { fakeProvider } from "../helpers.js";

const now = () => new Date("2026-10-04T00:00:00.000Z");
function seed(db: ReturnType<typeof openDb>, id: string, prompt: string, inputJson?: string) {
  for (const axis of ALL_AXES) {
    upsertEvalItem(db, { id: `ps-${id}-${axis}`, question: sortQuestionName(axis), input_json: inputJson ?? JSON.stringify({ prompt }), source: `decision:${id}:${axis}`, model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
  }
}
const reply = (over: Record<string, string> = {}) => ({
  status: "decided" as const, decision: "ok", confidence: 1, reason_code: "claude-cli",
  extra: { is_task: "yes", wants_loop: "no", scope_defined: "no", limits_defined: "no", approach_defined: "no", verification_defined: "no", is_bug_report: "yes", touches_ui: "yes", needs_research: "no", complexity: "small", ambiguity: "vague", ...over },
});

describe("adjudicateSort", () => {
  it("asks ONE question per prompt per sample for all 11 axes and stores strict 2-of-3 majorities", async () => {
    const db = openDb(":memory:");
    seed(db, "a", "Fix the login button crash");
    const seen: string[] = [];
    const script = [reply(), reply({ touches_ui: "no" }), reply({ complexity: "large" })];
    let n = 0;
    const provider = fakeProvider("claude-cli", ["brief"], (q) => { seen.push(q.prompt); return script[n++ % 3] as never; });
    expect(await adjudicateSort(db, { provider, limit: 10, now })).toEqual({ prompts: 1, labeled: 11, split: 0, failed: 0 });
    expect(seen).toHaveLength(3);
    expect(seen[0]).toContain("<prompt>\nFix the login button crash\n</prompt>");
    expect(labeledItems(db, "prompt-sort:touches_ui", "adjudicator").map((r) => r.label)).toEqual(["yes"]);
    expect(labeledItems(db, "prompt-sort:complexity", "adjudicator").map((r) => r.label)).toEqual(["small"]);
  });

  it("never shows the adjudicator the sorter's decision or confidence", async () => {
    const db = openDb(":memory:");
    seed(db, "a", "Fix the login button crash");
    const seen: string[] = [];
    await adjudicateSort(db, { provider: fakeProvider("claude-cli", ["brief"], (q) => { seen.push(q.prompt); return reply() as never; }), limit: 1, now });
    expect(seen[0]).not.toMatch(/confidence|probabilit|model_decision/i);
  });

  it("stores no label for a split axis, an off-enum answer or a failed call, and re-asks only prompts that still have unlabeled axes", async () => {
    const db = openDb(":memory:");
    seed(db, "a", "p1");
    seed(db, "b", "p2");
    let n = 0;
    const err = { status: "error" as const, reason_code: "exit-1" };
    const script = [reply({ complexity: "small" }), reply({ complexity: "large" }), reply({ complexity: "trivial", touches_ui: "maybe" }), err, err, err];
    const provider = fakeProvider("claude-cli", ["brief"], () => script[n++] as never);
    const r = await adjudicateSort(db, { provider, limit: 10, now });
    expect(r).toEqual({ prompts: 2, labeled: 10, split: 1, failed: 1 });
    expect(labeledItems(db, "prompt-sort:complexity", "adjudicator")).toEqual([]);
    expect(labeledItems(db, "prompt-sort:touches_ui", "adjudicator").map((x) => x.label)).toEqual(["yes"]);

    const again = await adjudicateSort(db, { provider: fakeProvider("claude-cli", ["brief"], reply() as never), limit: 10, now });
    expect(again).toEqual({ prompts: 2, labeled: 12, split: 0, failed: 0 });
    expect(labeledItems(db, "prompt-sort:complexity", "adjudicator").map((x) => x.label)).toEqual(["small", "small"]);
    expect((await adjudicateSort(db, { provider: fakeProvider("claude-cli", ["brief"], reply() as never), limit: 10, now })).prompts).toBe(0);
  });

  it("respects the limit and the answer vocabulary of every axis", async () => {
    const db = openDb(":memory:");
    seed(db, "a", "p1");
    seed(db, "b", "p2");
    const provider = fakeProvider("claude-cli", ["brief"], reply() as never);
    expect((await adjudicateSort(db, { provider, limit: 1, now })).prompts).toBe(1);
    for (const axis of ALL_AXES) {
      for (const row of labeledItems(db, sortQuestionName(axis), "adjudicator")) expect(axisOutputs(axis)).toContain(row.label);
    }
  });

  it("skips items with an unparseable or empty prompt, and ignores non-sort items", async () => {
    const db = openDb(":memory:");
    seed(db, "bad", "", "not json");
    seed(db, "empty", "   ");
    seed(db, "noprompt", "", JSON.stringify({ other: 1 }));
    upsertEvalItem(db, { id: "z", question: "ask-check", input_json: "{}", source: "decision:z", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    expect(await adjudicateSort(db, { provider: fakeProvider("claude-cli", ["brief"], reply() as never), limit: 10, now })).toEqual({ prompts: 0, labeled: 0, split: 0, failed: 0 });
  });

  it("honors a custom sample count and never writes decisions", async () => {
    const db = openDb(":memory:");
    seed(db, "a", "p1");
    let calls = 0;
    const provider = fakeProvider("claude-cli", ["brief"], () => { calls++; return reply() as never; });
    expect(await adjudicateSort(db, { provider, limit: 1, samples: 1, now })).toMatchObject({ labeled: 11 });
    expect(calls).toBe(1);
    expect((db.prepare("SELECT COUNT(*) AS n FROM decisions").get() as { n: number }).n).toBe(0);
  });
});
