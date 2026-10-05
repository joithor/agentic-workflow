// judge/tests/prompt-sort/sort.test.ts
import { describe, expect, it, vi } from "vitest";

import { STATE_CAP, sortPrompt } from "../../src/prompt-sort/sort.js";
import { deps, jevBody, jevFetch, noul } from "./fixtures.js";

const PROMPT = "Fix the login button crash on the settings page";
const sent = (fetch: ReturnType<typeof vi.fn>) =>
  JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { state: string; questions: Record<string, unknown> };

describe("sortPrompt", () => {
  it("asks all 11 questions in ONE request and blends the decisive answers", async () => {
    const fetch = jevFetch(jevBody());
    const out = await sortPrompt(PROMPT, { jev: deps(fetch), budgetMs: 1000 });
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(Object.keys(sent(fetch).questions)).toHaveLength(11);
    expect(out).toMatchObject({ mode: "blend", reason: "sorted", failure: null, judgeDecided: 11, usage: { input_tokens: 300, output_tokens: 20 } });
    expect(out.values).toMatchObject({ is_task: true, is_bug_report: true, touches_ui: true, complexity: "small", ambiguity: "vague" });
  });

  it("redacts secrets and caps the prompt before it leaves the machine (RF-1)", async () => {
    const fetch = jevFetch(jevBody());
    const secret = "sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345";
    const out = await sortPrompt(`fix auth, token ${secret} ${"x".repeat(STATE_CAP * 2)}`, { jev: deps(fetch), budgetMs: 1000 });
    expect(sent(fetch).state).not.toContain(secret);
    expect(sent(fetch).state).toContain("[REDACTED]");
    expect(sent(fetch).state).toHaveLength(STATE_CAP);
    expect(out.sentPrompt).toBe(sent(fetch).state);
  });

  it("keeps the heuristic for an axis Jev did not answer and for an undecided band (RF-2)", async () => {
    const body = jevBody({ is_task: noul(0.5) });
    delete (body.answers as Record<string, unknown>).touches_ui;
    const out = await sortPrompt(PROMPT, { jev: deps(jevFetch(body)), budgetMs: 1000 });
    expect(out.judgeDecided).toBe(9);
    expect(out.axes.find((a) => a.axis === "touches_ui")).toMatchObject({ source: "heuristic", status: "undecided", value: true });
    expect(out.axes.find((a) => a.axis === "is_task")).toMatchObject({ source: "heuristic", status: "undecided" });
  });

  it.each([
    ["http 503", () => jevFetch({}, 503), "http-503", "http-503"],
    ["timeout", () => vi.fn().mockRejectedValue(new DOMException("aborted", "AbortError")), "timeout", "timeout"],
    ["network error", () => vi.fn().mockRejectedValue(new Error("ECONNRESET")), "network-error", null],
  ])("falls back to pure heuristics on %s (RF-2)", async (_name, make, reason, failure) => {
    const out = await sortPrompt(PROMPT, { jev: deps(make()), budgetMs: 1000 });
    expect(out).toMatchObject({ mode: "heuristic-only", reason, failure, judgeDecided: 0, usage: null });
    expect(out.values.is_bug_report).toBe(true);
  });

  it("is heuristic-only with no key (not a failure), no Jev deps, or heuristic mode", async () => {
    const noKey = await sortPrompt(PROMPT, { jev: { fetch: vi.fn(), apiKey: async () => null }, budgetMs: 1000 });
    expect(noKey).toMatchObject({ mode: "heuristic-only", reason: "no-api-key", failure: null });
    expect(await sortPrompt(PROMPT, { jev: null, budgetMs: 1000 })).toMatchObject({ reason: "no-jev", failure: null });
    const fetch = jevFetch(jevBody());
    expect(await sortPrompt(PROMPT, { jev: deps(fetch), budgetMs: 1000 }, "heuristic")).toMatchObject({ reason: "heuristic-mode" });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("reports all-undecided when every axis sits in the band, and measures latency with the injected clock", async () => {
    const answers = Object.fromEntries(Object.keys(jevBody().answers).map((k) => [k, k === "complexity" || k === "ambiguity" ? { type: "score", score: 0, probabilities: {}, confidence: 0.1 } : noul(0.5)]));
    let t = 0;
    const out = await sortPrompt(PROMPT, { jev: deps(jevFetch({ answers })), budgetMs: 1000, clock: () => (t += 40) });
    expect(out).toMatchObject({ mode: "heuristic-only", reason: "all-undecided", judgeDecided: 0, latencyMs: 40 });
  });
});
