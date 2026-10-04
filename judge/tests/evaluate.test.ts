import crypto from "node:crypto";
import { z } from "zod";
import { describe, expect, it, vi } from "vitest";

import { evaluate } from "../src/evaluate.js";
import type { QuestionModule } from "../src/question.js";
import { openDb, getDecision, getDecisionDetails } from "../src/db.js";
import { DEFAULT_CONFIG } from "../src/config.js";
import { DEFAULT_CHAIN } from "../src/chain.js";
import { fakeProvider } from "./helpers.js";

const InputSchema = z.object({ text: z.string() });
type Input = z.infer<typeof InputSchema>;
type Output = "send" | "batch" | "drop";

function question(overrides: Partial<QuestionModule<Input, Output>> = {}): QuestionModule<Input, Output> {
  return {
    name: "wake-gate",
    inputSchema: InputSchema,
    outputs: ["send", "batch", "drop"],
    prompt: (i) => `Classify: ${i.text}`,
    threshold: 0.7,
    contentClass: "message-meta",
    timeBudgetMs: 5000,
    ...overrides,
  };
}

describe("evaluate", () => {
  it("settles on a pre-rule without calling any provider (RF-5)", async () => {
    const db = openDb(":memory:");
    const q = question({ preRules: (i) => (i.text === "{}" ? "drop" : null) });
    const called: string[] = [];
    const provider = fakeProvider("claude-cli", ["message-meta"], () => {
      called.push("claude-cli");
      return { status: "decided", decision: "send", confidence: 1, reason_code: "model" };
    });
    const result = await evaluate(q, { text: "{}" }, { db, config: DEFAULT_CONFIG, providers: [provider] });
    expect(result).toMatchObject({ decision: "drop", model: "rules" });
    expect(called).toEqual([]);
  });

  it("walks the chain in order and uses the first decided result", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-key" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "batch", confidence: 0.9, reason_code: "model" });
    const result = await evaluate(q, { text: "progress update" }, { db, config: DEFAULT_CONFIG, providers: [jev, cli] });
    expect(result).toMatchObject({ decision: "batch", model: "claude-cli", confidence: 0.9 });
  });

  it("escalates, never guesses, when every provider is unavailable (below threshold)", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-key" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "error", reason_code: "timeout" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [jev, cli] });
    expect(result).toEqual({ escalate: true, reason_code: "no-provider-decided" });
  });

  it("escalates when a decided result is below the question's threshold", async () => {
    const db = openDb(":memory:");
    const q = question({ threshold: 0.95 });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 0.5, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [cli] });
    expect(result).toEqual({ escalate: true, reason_code: "below-threshold" });
  });

  it("rejects an out-of-enum decision as a provider error and falls through (RF-3)", async () => {
    const db = openDb(":memory:");
    const q = question();
    const bad = fakeProvider("jev", ["message-meta"], { status: "decided", decision: "yes" as unknown as Output, confidence: 1, reason_code: "model" });
    const good = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [bad, good], randomId: () => "oo-id" });
    expect(result).toMatchObject({ decision: "send", model: "claude-cli" });
    const failures = db.prepare("SELECT COUNT(*) as n FROM failures WHERE provider = 'jev'").get();
    expect(failures).toEqual({ n: 1 });
    // The winner is chain position 1 (jev was position 0 and was skipped), and
    // the stored row records exactly why jev was passed over (spec: real
    // fallback accounting, not conflated with the failures table).
    expect(getDecision(db, "oo-id")).toMatchObject({
      chain_position: 1,
      skipped: [{ provider: "jev", reason: "failed" }],
    });
  });

  it("records a failure row and fails open when a provider throws (RF-1)", async () => {
    const db = openDb(":memory:");
    const q = question();
    const throwing = fakeProvider<Output>("claude-cli", ["message-meta"], () => {
      throw new Error("child killed");
    });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [throwing] });
    expect(result).toEqual({ escalate: true, reason_code: "no-provider-decided" });
    const failures = db.prepare("SELECT reason_code FROM failures").all();
    expect(failures).toEqual([{ reason_code: "provider-threw" }]);
  });

  it("skips a disabled question entirely and escalates", async () => {
    const db = openDb(":memory:");
    const q = question();
    const config = { questions: { "wake-gate": { enabled: false, threshold: 0.7 } } };
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], () => {
      throw new Error("must not be called");
    });
    const result = await evaluate(q, { text: "hmm" }, { db, config, providers: [cli] });
    expect(result).toEqual({ escalate: true, reason_code: "question-disabled" });
  });

  it("rejects input that fails the question's Zod schema before any provider runs", async () => {
    const db = openDb(":memory:");
    const q = question();
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], () => {
      throw new Error("must not be called");
    });
    const result = await evaluate(q, { text: 5 }, { db, config: DEFAULT_CONFIG, providers: [cli] });
    expect(result).toEqual({ escalate: true, reason_code: "invalid-input" });
  });

  it("records the winning decision in decisions.sqlite with a generated id, chain_position 0, and no skipped entries when the first candidate decides", async () => {
    const db = openDb(":memory:");
    const q = question();
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [cli], now: () => new Date("2026-09-27T00:00:00.000Z"), randomId: () => "fixed-id" });
    expect(result).toMatchObject({ id: "fixed-id" });
    expect(getDecision(db, "fixed-id")).toMatchObject({ decision: "send", provider: "claude-cli", question: "wake-gate", content_class: "message-meta", chain_position: 0, skipped: [] });
  });

  it("records every skipped provider's reason (unavailable, timeout, below_threshold) ahead of the winner", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "batch", confidence: 0.9, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [jev, cli], randomId: () => "skip-id" });
    expect(result).toMatchObject({ decision: "batch" });
    expect(getDecision(db, "skip-id")).toMatchObject({
      chain_position: 1,
      skipped: [{ provider: "jev", reason: "unavailable" }],
    });
  });

  it("records a skipped reason of timeout, not unavailable, when the provider's own reason_code is timeout", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "timeout" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [jev, cli], randomId: () => "timeout-skip-id" });
    expect(result).toMatchObject({ decision: "send" });
    expect(getDecision(db, "timeout-skip-id")).toMatchObject({ skipped: [{ provider: "jev", reason: "timeout" }] });
  });

  it("a failed failure-row write inside the chain still escalates without throwing", async () => {
    const db = openDb(":memory:");
    db.close(); // any write now throws, including recordFailureSafe's own attempt
    const q = question();
    const erroring = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "error", reason_code: "timeout" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [erroring] });
    expect(result).toEqual({ escalate: true, reason_code: "no-provider-decided" });
  });

  it("a failed decision write still returns the decision (fails open, counted as a judge failure)", async () => {
    const db = openDb(":memory:");
    db.close(); // any write now throws
    const q = question();
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "model" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [cli] });
    expect(result).toMatchObject({ decision: "send" });
  });

  it("records a decisions row for a disabled question, outcome escalated, decision null, provider none", async () => {
    const db = openDb(":memory:");
    const q = question();
    const config = { questions: { "wake-gate": { enabled: false, threshold: 0.7 } } };
    const result = await evaluate(q, { text: "hmm" }, { db, config, providers: [], randomId: () => "disabled-id" });
    expect(result).toEqual({ escalate: true, reason_code: "question-disabled" });
    expect(getDecision(db, "disabled-id")).toMatchObject({ decision: null, provider: "none", outcome: "escalated", reason_code: "question-disabled" });
  });

  it("records a decisions row for invalid input, outcome escalated, decision null", async () => {
    const db = openDb(":memory:");
    const q = question();
    const result = await evaluate(q, { text: 5 }, { db, config: DEFAULT_CONFIG, providers: [], randomId: () => "invalid-id" });
    expect(result).toEqual({ escalate: true, reason_code: "invalid-input" });
    expect(getDecision(db, "invalid-id")).toMatchObject({ decision: null, provider: "none", outcome: "escalated", reason_code: "invalid-input" });
  });

  it("records outcome escalated (not failed) when nothing decided but no provider actually failed or timed out", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [jev], randomId: () => "clean-escalate-id" });
    expect(result).toEqual({ escalate: true, reason_code: "no-provider-decided" });
    expect(getDecision(db, "clean-escalate-id")).toMatchObject({ outcome: "escalated", decision: null });
  });

  it("records outcome failed (a real provider failure/timeout occurred) when nothing decided (matches the real-world case: claude-cli timed out)", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "unavailable", reason_code: "timeout" });
    const result = await evaluate(q, { text: "hmm" }, { db, config: DEFAULT_CONFIG, providers: [jev, cli], randomId: () => "real-failure-id" });
    expect(result).toEqual({ escalate: true, reason_code: "no-provider-decided" });
    expect(getDecision(db, "real-failure-id")).toMatchObject({
      outcome: "failed", decision: null,
      skipped: [{ provider: "jev", reason: "unavailable" }, { provider: "claude-cli", reason: "timeout" }],
    });
    // A real timeout is now counted as a judge failure (previously silently
    // dropped) — this is the bug the user hit: `judge health` said 0 failures.
    const failures = db.prepare("SELECT COUNT(*) as n FROM failures WHERE provider = 'claude-cli' AND reason_code = 'timeout'").get();
    expect(failures).toEqual({ n: 1 });
  });

  it("records outcome failed when below-threshold is reached only after a real failure earlier in the chain", async () => {
    const db = openDb(":memory:");
    const q = question({ threshold: 0.95 });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "error", reason_code: "exit-1" });
    const rules = fakeProvider<Output>("rules", ["message-meta"], { status: "decided", decision: "send", confidence: 0.5, reason_code: "rules-fallback" });
    const result = await evaluate(q, { text: "hmm" }, {
      db, config: DEFAULT_CONFIG, providers: [cli, rules], chain: DEFAULT_CHAIN, randomId: () => "mixed-failure-id",
    });
    expect(result).toEqual({ escalate: true, reason_code: "below-threshold" });
    expect(getDecision(db, "mixed-failure-id")).toMatchObject({ outcome: "failed" });
  });

  it("reaches rules at the end of the chain when every model provider fails or is unavailable (plan review BLOCKER: rules was unreachable)", async () => {
    const db = openDb(":memory:");
    const q = question();
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" });
    const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "error", reason_code: "timeout" });
    const rules = fakeProvider<Output>("rules", ["message-meta"], { status: "decided", decision: "batch", confidence: 1, reason_code: "rules-fallback" });
    const result = await evaluate(q, { text: "hmm" }, {
      db, config: DEFAULT_CONFIG, providers: [jev, cli, rules], chain: DEFAULT_CHAIN, randomId: () => "rules-id",
    });
    expect(result).toMatchObject({ decision: "batch", model: "rules", reason_code: "rules-fallback" });
    expect(getDecision(db, "rules-id")).toMatchObject({
      chain_position: 2,
      skipped: [{ provider: "jev", reason: "unavailable" }, { provider: "claude-cli", reason: "failed" }],
    });
  });

  const decidedJev = (): ReturnType<typeof fakeProvider<Output>> =>
    fakeProvider<Output>("jev", ["message-meta"], { status: "decided", decision: "send", confidence: 0.9, reason_code: "jev" });
  const jevChain = { classes: { "message-meta": ["jev" as const] } };

  it("stores the redacted, capped input in decision_details and leaves input_digest on the unredacted input (RF-1, RF-3)", async () => {
    const db = openDb(":memory:");
    const input = { text: "token sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345 here" };
    const out = await evaluate(question(), input, { db, config: DEFAULT_CONFIG, providers: [decidedJev()], chain: jevChain, randomId: () => "id1" });
    expect("escalate" in out).toBe(false);
    expect(getDecisionDetails(db, "id1")?.input_json).toBe('{"text":"token [REDACTED] here"}');
    const expectedDigest = crypto.createHash("sha256").update(JSON.stringify(input)).digest("hex").slice(0, 16);
    expect(getDecision(db, "id1")?.input_digest).toBe(expectedDigest);
  });

  it("stores the provider's probability distribution in decision_details", async () => {
    const db = openDb(":memory:");
    const jev = fakeProvider<Output>("jev", ["message-meta"], { status: "decided", decision: "send", confidence: 0.9, reason_code: "jev", probabilities: { send: 0.9, batch: 0.1 } });
    await evaluate(question(), { text: "hi" }, { db, config: DEFAULT_CONFIG, providers: [jev], chain: jevChain, randomId: () => "idp" });
    expect(getDecisionDetails(db, "idp")?.probabilities).toEqual({ send: 0.9, batch: 0.1 });
  });

  it.each([["\n"], ["\t"]])("redacts a secret that follows an escaped whitespace char inside a string value (%j)", async (ws) => {
    const db = openDb(":memory:");
    const input = { text: `KEY=${ws}sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345${ws}AKIAABCDEFGHIJKLMNOP` };
    await evaluate(question(), input, { db, config: DEFAULT_CONFIG, providers: [decidedJev()], chain: jevChain, randomId: () => "idws" });
    const stored = getDecisionDetails(db, "idws")?.input_json ?? "";
    expect(stored).not.toContain("sk-ant");
    expect(stored).not.toContain("AKIA");
    expect(JSON.parse(stored)).toEqual({ text: `KEY=${ws}[REDACTED]${ws}[REDACTED]` });
    const expectedDigest = crypto.createHash("sha256").update(JSON.stringify(input)).digest("hex").slice(0, 16);
    expect(getDecision(db, "idws")?.input_digest).toBe(expectedDigest);
  });

  it("redacts before capping so a secret cut at the cap boundary cannot survive", async () => {
    const db = openDb(":memory:");
    const secret = "sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345";
    // '{"text":"' is 9 chars; capping first would cut the secret at 10 chars,
    // too short to match any pattern. Redact-first leaves nothing to leak.
    const text = `${"z".repeat(16000 - 9 - 1 - 10)} ${secret} ${"y".repeat(100)}`;
    await evaluate(question(), { text }, { db, config: DEFAULT_CONFIG, providers: [decidedJev()], chain: jevChain, randomId: () => "id3" });
    const stored = getDecisionDetails(db, "id3")?.input_json ?? "";
    expect(stored).not.toContain("sk-ant");
    expect(stored).toMatch(/\[truncated \d+ chars\]$/);
  });

  it("records the session id from deps alongside the details", async () => {
    const db = openDb(":memory:");
    await evaluate(question(), { text: "hi" }, { db, config: DEFAULT_CONFIG, providers: [decidedJev()], chain: jevChain, randomId: () => "id4", sessionId: "s1" });
    expect(getDecisionDetails(db, "id4")?.session_id).toBe("s1");
  });

  it("still returns the decision when the details write throws", async () => {
    const db = openDb(":memory:");
    db.exec("DROP TABLE decision_details");
    const out = await evaluate(question(), { text: "hi" }, { db, config: DEFAULT_CONFIG, providers: [decidedJev()], chain: jevChain, randomId: () => "id2" });
    expect(out).toMatchObject({ decision: "send", id: "id2" });
    expect(getDecision(db, "id2")?.decision).toBe("send");
  });

  describe("undecided answers", () => {
    const below = { status: "decided", decision: "batch", confidence: 0.55, reason_code: "jev", probabilities: { send: 0.3, batch: 0.55, drop: 0.15 } } as const;
    const failureCount = (db: ReturnType<typeof openDb>): number => (db.prepare("SELECT COUNT(*) n FROM failures").get() as { n: number }).n;

    it("settles with fallbackRules when a provider is below threshold, without calling the slower provider", async () => {
      const db = openDb(":memory:");
      const slow = vi.fn();
      const q = question({ threshold: 0.8, fallbackRules: () => "send" });
      const out = await evaluate(q, { text: "hi" }, {
        db, config: DEFAULT_CONFIG, randomId: () => "u1",
        providers: [fakeProvider<Output>("jev", ["message-meta"], below), { name: "claude-cli", classes: new Set(["message-meta"]), decide: slow }],
        chain: { classes: { "message-meta": ["jev", "claude-cli"] } },
      });
      expect(out).toMatchObject({ decision: "send", model: "rules", reason_code: "fallback-after-undecided" });
      expect(slow).not.toHaveBeenCalled();
      expect(getDecision(db, "u1")).toMatchObject({ provider: "rules", skipped: [{ provider: "jev", reason: "below_threshold" }] });
      expect(getDecisionDetails(db, "u1")).toMatchObject({ agreement: "undecided" });
      expect(getDecisionDetails(db, "u1")?.probabilities).toEqual(below.probabilities);
      expect(failureCount(db)).toBe(0);
    });

    it("continues the chain when fallbackRules return null, keeping the undecided probabilities", async () => {
      const db = openDb(":memory:");
      const q = question({ threshold: 0.8, fallbackRules: () => null });
      const cli = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "drop", confidence: 0.9, reason_code: "model" });
      const out = await evaluate(q, { text: "hi" }, {
        db, config: DEFAULT_CONFIG, randomId: () => "u2",
        providers: [fakeProvider<Output>("jev", ["message-meta"], below), cli],
        chain: { classes: { "message-meta": ["jev", "claude-cli"] } },
      });
      expect(out).toMatchObject({ decision: "drop", model: "claude-cli" });
      expect(getDecisionDetails(db, "u2")?.probabilities).toEqual(below.probabilities);
    });

    it("prefers the settling provider's own probabilities over an earlier undecided answer", async () => {
      const db = openDb(":memory:");
      const second = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "drop", confidence: 0.9, reason_code: "m", probabilities: { drop: 0.9 } });
      await evaluate(question({ threshold: 0.8 }), { text: "hi" }, {
        db, config: DEFAULT_CONFIG, randomId: () => "u3",
        providers: [fakeProvider<Output>("jev", ["message-meta"], below), second],
        chain: { classes: { "message-meta": ["jev", "claude-cli"] } },
      });
      expect(getDecisionDetails(db, "u3")?.probabilities).toEqual({ drop: 0.9 });
    });

    it("records the undecided probabilities when the whole chain escalates, and no failures row", async () => {
      const db = openDb(":memory:");
      const out = await evaluate(question({ threshold: 0.8 }), { text: "hi" }, {
        db, config: DEFAULT_CONFIG, randomId: () => "u4",
        providers: [fakeProvider<Output>("jev", ["message-meta"], below)],
        chain: { classes: { "message-meta": ["jev"] } },
      });
      expect(out).toEqual({ escalate: true, reason_code: "below-threshold" });
      expect(getDecisionDetails(db, "u4")?.probabilities).toEqual(below.probabilities);
      expect(failureCount(db)).toBe(0);
    });

    it("keeps an undecided answer without probabilities from clobbering earlier ones", async () => {
      const db = openDb(":memory:");
      const bare = fakeProvider<Output>("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 0.1, reason_code: "m" });
      await evaluate(question({ threshold: 0.8 }), { text: "hi" }, {
        db, config: DEFAULT_CONFIG, randomId: () => "u5",
        providers: [fakeProvider<Output>("jev", ["message-meta"], below), bare],
        chain: { classes: { "message-meta": ["jev", "claude-cli"] } },
      });
      expect(getDecisionDetails(db, "u5")?.probabilities).toEqual(below.probabilities);
    });

    it("restricts and reorders the chain to a question's configured providers", async () => {
      const db = openDb(":memory:");
      const cli = vi.fn();
      const out = await evaluate(question(), { text: "hi" }, {
        db, config: { questions: { "wake-gate": { enabled: true, threshold: 0.7, providers: ["jev", "rules"] } } },
        providers: [
          fakeProvider<Output>("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" }),
          { name: "claude-cli", classes: new Set(["message-meta"]), decide: cli } as Provider,
        ],
        chain: { classes: { "message-meta": ["jev", "claude-cli", "rules"] } },
      });
      expect(cli).not.toHaveBeenCalled();
      expect(out).toMatchObject({ escalate: true });
    });
  });
});
