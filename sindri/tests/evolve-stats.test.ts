import { describe, expect, it } from "vitest";

import { askModel } from "../src/evolve/ask.js";
import { wilsonLower } from "../src/evolve/stats.js";
import { SindriError } from "../src/errors.js";
import { Budget, ModelAnswerError, type ModelCall, type ModelRunner } from "../src/scope/model.js";

describe("wilsonLower", () => {
  it("matches known values", () => {
    expect(wilsonLower(0, 0)).toBe(0);
    expect(wilsonLower(14, 19)).toBeCloseTo(0.512, 2);
    expect(wilsonLower(20, 20)).toBeCloseTo(0.839, 2);
    expect(wilsonLower(12, 12)).toBeCloseTo(0.758, 2);
  });
});

describe("askModel", () => {
  const call: ModelCall<number> = { role: "draft", model: "m", system: "s", input: "i", schema: {}, parse: (v) => Number(v), timeoutMs: 1 };
  const runner = (fn: () => number): ModelRunner => ({
    async run<T>(c: ModelCall<T>) {
      return { value: c.parse(fn()), usage: { inputTokens: 3, outputTokens: 2 } };
    },
  });

  it("spends usage on success", async () => {
    const budget = new Budget(100);
    expect(await askModel(runner(() => 7), budget, call)).toEqual({ ok: true, value: 7 });
    expect(budget.used).toBe(5);
  });

  it("refuses when the budget is exhausted, without calling the model", async () => {
    const budget = new Budget(1);
    budget.spend({ inputTokens: 1, outputTokens: 0 });
    let called = false;
    const r = await askModel(runner(() => { called = true; return 1; }), budget, call);
    expect(r).toEqual({ ok: false, why: "token budget exhausted" });
    expect(called).toBe(false);
  });

  it("turns model failures and schema mismatches into a result, and rethrows anything else", async () => {
    const failing = (code: "SND-SCOPE-002" | "SND-SCOPE-004"): ModelRunner => ({ run: async () => { throw new SindriError(code, "bad answer"); } });
    expect(await askModel(failing("SND-SCOPE-002"), new Budget(10), call)).toEqual({ ok: false, why: "bad answer" });
    expect(await askModel(failing("SND-SCOPE-004"), new Budget(10), call)).toEqual({ ok: false, why: "bad answer" });
    const bug: ModelRunner = { run: async () => { throw new SindriError("SND-CLI-002", "not a model failure"); } };
    await expect(askModel(bug, new Budget(10), call)).rejects.toThrow("not a model failure");
    const plain: ModelRunner = { run: async () => { throw new Error("boom"); } };
    await expect(askModel(plain, new Budget(10), call)).rejects.toThrow("boom");
  });

  it("charges the usage of a failed or invalid answer to the budget", async () => {
    const budget = new Budget(100);
    const invalid: ModelRunner = { run: async () => { throw new ModelAnswerError("junk", { inputTokens: 4, outputTokens: 6 }); } };
    expect(await askModel(invalid, budget, call)).toEqual({ ok: false, why: "junk" });
    expect(budget.used).toBe(10);
  });
});
