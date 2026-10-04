import { describe, expect, it, vi } from "vitest";

import { callJev, type JevQuestion } from "../../src/providers/jev-api.js";

const ok = (body: unknown) => vi.fn().mockResolvedValue({ status: 200, json: async () => body });

const questions: Record<string, JevQuestion> = {
  is_task: { type: "noul", instructions: "Is this a task?" },
  tier: { type: "score", instructions: "How big?", criteria: ["trivial", "small", "large"] },
  kind: { type: "choice", instructions: "Which kind?", criteria: { bug: "a defect report", feature: "new behaviour" } },
};

describe("callJev", () => {
  it("sends every question in one request and returns typed answers plus usage", async () => {
    const fetch = ok({
      answers: {
        is_task: { type: "noul", noul: 0.93 },
        tier: { type: "score", score: 1.2, probabilities: { trivial: 0.1, small: 0.6, large: 0.3 }, confidence: 0.6 },
        kind: { type: "choice", choice: "bug", probabilities: { bug: 0.8, feature: 0.2 }, confidence: 0.8 },
      },
      usage: { input_tokens: 120, output_tokens: 9 },
    });
    const out = await callJev({ fetch, apiKey: async () => "k" }, { prompt: "fix it" }, questions, 1000);
    expect(fetch).toHaveBeenCalledTimes(1);
    const body = JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { questions: Record<string, unknown>; model: string };
    expect(Object.keys(body.questions)).toEqual(["is_task", "tier", "kind"]);
    expect(body.model).toBe("jev-1.13.0");
    expect(out).toEqual({
      status: "ok",
      usage: { input_tokens: 120, output_tokens: 9 },
      answers: {
        is_task: { ok: true, answer: { type: "noul", noul: 0.93 } },
        tier: { ok: true, answer: { type: "score", score: 1.2, probabilities: { trivial: 0.1, small: 0.6, large: 0.3 }, confidence: 0.6 } },
        kind: { ok: true, answer: { type: "choice", choice: "bug", probabilities: { bug: 0.8, feature: 0.2 }, confidence: 0.8 } },
      },
    });
  });

  it("reports a missing or malformed answer per question, keeping the rest (RF-5)", async () => {
    const fetch = ok({ answers: { is_task: { type: "noul", noul: 0.2 }, tier: { type: "score" } }, usage: { input_tokens: 1, output_tokens: 1 } });
    const out = await callJev({ fetch, apiKey: async () => "k" }, "s", questions, 1000);
    expect(out.status === "ok" && out.answers).toEqual({
      is_task: { ok: true, answer: { type: "noul", noul: 0.2 } },
      tier: { ok: false, reason_code: "unparseable-result" },
      kind: { ok: false, reason_code: "unparseable-result" },
    });
  });

  it("refuses >255 choice options or >10 score levels before calling fetch", async () => {
    const fetch = vi.fn();
    const many = Object.fromEntries(Array.from({ length: 256 }, (_, i) => [`o${i}`, `o${i}`]));
    expect(await callJev({ fetch, apiKey: async () => "k" }, "s", { q: { type: "choice", instructions: "", criteria: many } }, 1000))
      .toEqual({ status: "error", reason_code: "too-many-options" });
    expect(await callJev({ fetch, apiKey: async () => "k" }, "s", { q: { type: "score", instructions: "", criteria: Array.from({ length: 11 }, (_, i) => `l${i}`) } }, 1000))
      .toEqual({ status: "error", reason_code: "too-many-levels" });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("is unavailable without a key, on timeout, and on network error; an error on non-2xx", async () => {
    expect(await callJev({ fetch: vi.fn(), apiKey: async () => null }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "no-api-key" });
    const abort = vi.fn().mockRejectedValue(new DOMException("aborted", "AbortError"));
    expect(await callJev({ fetch: abort, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "timeout" });
    const net = vi.fn().mockRejectedValue(new Error("ECONNRESET"));
    expect(await callJev({ fetch: net, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "network-error" });
    const http = vi.fn().mockResolvedValue({ status: 503, json: async () => ({}) });
    expect(await callJev({ fetch: http, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "error", reason_code: "http-503" });
  });

  it("defaults usage to zeros when the response omits it, and honours a model override", async () => {
    const fetch = ok({ answers: { is_task: { type: "noul", noul: 1 } } });
    const out = await callJev({ fetch, apiKey: async () => "k", model: "jev-latest" }, "s", { is_task: questions.is_task as JevQuestion }, 1000);
    expect(out).toMatchObject({ status: "ok", usage: { input_tokens: 0, output_tokens: 0 } });
    expect((JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { model: string }).model).toBe("jev-latest");
  });

  it("fills probabilities and confidence defaults for score and choice answers that omit them", async () => {
    const fetch = ok({
      answers: {
        tier: { type: "score", score: 2 },
        kind: { type: "choice", choice: "bug", probabilities: "nope", confidence: "high" },
      },
    });
    const out = await callJev({ fetch, apiKey: async () => "k" }, "s", { tier: questions.tier as JevQuestion, kind: questions.kind as JevQuestion }, 1000);
    expect(out.status === "ok" && out.answers).toEqual({
      tier: { ok: true, answer: { type: "score", score: 2, probabilities: {}, confidence: 1 } },
      kind: { ok: true, answer: { type: "choice", choice: "bug", probabilities: {}, confidence: 1 } },
    });
  });

  it("rejects non-object answers, a mismatched type, a choice outside criteria, and non-numeric noul", async () => {
    const fetch = ok({
      answers: {
        a: null,
        b: { type: "choice", choice: "bug", probabilities: {}, confidence: 1 },
        c: { type: "choice", choice: "other" },
        d: { type: "noul", noul: "x" },
        e: { type: "score", score: "x" },
      },
    });
    const qs: Record<string, JevQuestion> = {
      a: questions.is_task as JevQuestion, b: questions.is_task as JevQuestion, c: questions.kind as JevQuestion,
      d: questions.is_task as JevQuestion, e: questions.tier as JevQuestion,
    };
    const out = await callJev({ fetch, apiKey: async () => "k" }, "s", qs, 1000);
    const bad = { ok: false, reason_code: "unparseable-result" };
    expect(out.status === "ok" && out.answers).toEqual({ a: bad, b: bad, c: bad, d: bad, e: bad });
  });
});
