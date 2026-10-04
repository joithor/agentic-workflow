import { z } from "zod";
import { describe, expect, it } from "vitest";

import { adjudicate } from "../src/adjudicate.js";
import { labeledItems, openDb, upsertEvalItem } from "../src/db.js";
import type { QuestionModule } from "../src/question.js";
import { fakeProvider } from "./helpers.js";

const now = () => new Date("2026-10-04T00:00:00.000Z");
const SENTINEL = "SENTINEL-MODEL-DECISION-7f3a";
const item = (id: string, model_decision = "send") => ({ id, question: "wake-gate", input_json: '{"text":"step 2 done","senderKind":"teammate"}', source: `decision:${id}`, model_decision, created_at: "2026-10-01T00:00:00.000Z" });

describe("adjudicate", () => {
  it("stores a 2-of-3 majority as an adjudicator label and never shows the model decision", async () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, item("a", SENTINEL));
    const seen: string[] = [];
    let n = 0;
    const answers = ["batch", "batch", "send"];
    const provider = fakeProvider("claude-cli", ["message-meta"], (q, input) => {
      seen.push(q.prompt, JSON.stringify(input));
      return { status: "decided", decision: answers[n++ % 3] as string, confidence: 1, reason_code: "claude-cli" };
    });
    expect(await adjudicate(db, "wake-gate", { provider, limit: 10, now })).toEqual({ labeled: 1, split: 0, failed: 0 });
    expect(labeledItems(db, "wake-gate", "adjudicator").map((r) => r.label)).toEqual(["batch"]);
    expect(seen).toHaveLength(6);
    expect(seen.every((p) => !p.includes(SENTINEL) && !p.includes("model_decision"))).toBe(true);
    expect(seen[0]).toContain("step 2 done");
  });

  it("stores nothing on a three-way split, an off-enum answer, or a failed call (RF-4)", async () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b", "c"]) upsertEvalItem(db, item(id));
    const script: Record<string, string[]> = { a: ["send", "batch", "drop"], b: ["maybe", "maybe", "send"], c: [] };
    let calls = 0;
    const provider = fakeProvider("claude-cli", ["message-meta"], () => {
      const id = ["a", "b", "c"][Math.floor(calls / 3)] as string;
      const d = script[id]?.[calls++ % 3];
      return d === undefined ? { status: "error", reason_code: "exit-1" } : { status: "decided", decision: d, confidence: 1, reason_code: "x" };
    });
    expect(await adjudicate(db, "wake-gate", { provider, limit: 10, now })).toEqual({ labeled: 0, split: 2, failed: 1 });
    expect(labeledItems(db, "wake-gate")).toEqual([]);
  });

  it("errors on an unknown question and respects the limit", async () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, item(id));
    const provider = fakeProvider("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "x" });
    await expect(adjudicate(db, "nope", { provider, limit: 1, now })).rejects.toThrow("unknown question: nope");
    expect(await adjudicate(db, "wake-gate", { provider, limit: 1, now })).toEqual({ labeled: 1, split: 0, failed: 0 });
  });

  it("counts a stored input that is not valid JSON or fails the schema as failed, without calling the provider", async () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, { ...item("trunc"), input_json: '{"text":"cut off' });
    upsertEvalItem(db, { ...item("schema"), input_json: '{"nope":1}', created_at: "2026-10-02T00:00:00.000Z" });
    let calls = 0;
    const provider = fakeProvider("claude-cli", ["message-meta"], () => {
      calls++;
      return { status: "decided", decision: "send", confidence: 1, reason_code: "x" };
    });
    expect(await adjudicate(db, "wake-gate", { provider, limit: 10, now })).toEqual({ labeled: 0, split: 0, failed: 2 });
    expect(calls).toBe(0);
  });

  it("appends each option's criteria to the prompt when the question has them, and leaves the prompt alone otherwise", async () => {
    const mod = (criteria?: Record<"x" | "y", string>): QuestionModule<unknown, string> => ({
      name: "fake", inputSchema: z.object({ v: z.string() }), outputs: ["x", "y"], contentClass: "message-meta",
      threshold: 0.5, timeBudgetMs: 1000, prompt: () => "BASE PROMPT", ...(criteria === undefined ? {} : { criteria }),
    });
    const run = async (m: QuestionModule<unknown, string>): Promise<string> => {
      const db = openDb(":memory:");
      upsertEvalItem(db, { id: "f1", question: "fake", input_json: '{"v":"1"}', source: "decision:f1", model_decision: "x", created_at: "2026-10-01T00:00:00.000Z" });
      let prompt = "";
      const provider = fakeProvider("claude-cli", ["message-meta"], (q) => {
        prompt = q.prompt;
        return { status: "decided", decision: "x", confidence: 1, reason_code: "x" };
      });
      await adjudicate(db, "fake", { provider, limit: 1, now, questions: { fake: m } });
      return prompt;
    };
    expect(await run(mod())).toBe("BASE PROMPT");
    expect(await run(mod({ x: "means X", y: "means Y" }))).toBe("BASE PROMPT\n\nOptions:\n- x: means X\n- y: means Y");
  });
});
