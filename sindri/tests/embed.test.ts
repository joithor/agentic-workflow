import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { cosine, decodeVec, embeddingText, encodeVec, makeOllamaEmbedder } from "../src/index/embed.js";
import type { FetchLike } from "../src/index/io.js";

type Init = Parameters<FetchLike>[1];

function fakeFetch(answer: (body: { model: string; input: string[] }) => { ok: boolean; status: number; json: unknown }): FetchLike & { calls: string[]; inits: Init[] } {
  const calls: string[] = [];
  const inits: Init[] = [];
  const f = (async (url, init) => {
    calls.push(url);
    inits.push(init);
    const a = answer(JSON.parse(init.body) as { model: string; input: string[] });
    return { ok: a.ok, status: a.status, json: async () => a.json };
  }) as FetchLike & { calls: string[]; inits: Init[] };
  f.calls = calls;
  f.inits = inits;
  return f;
}

// A server that never answers: only the abort signal ends the request.
const hang: FetchLike = (_url, init) => new Promise((_resolve, reject) => init.signal.addEventListener("abort", () => reject(new Error("aborted"))));

describe("Ollama embedder (Review Focus 3)", () => {
  it("posts batches of 32 to /api/embed on loopback, refusing redirects, and returns vectors", async () => {
    const fetch = fakeFetch((b) => ({ ok: true, status: 200, json: { embeddings: b.input.map((_, i) => [i, 1, 0]) } }));
    const e = makeOllamaEmbedder({ url: "http://127.0.0.1:11434/", model: "nomic-embed-text", fetch });
    const out = await e.embed(Array.from({ length: 40 }, (_, i) => `t${i}`));
    expect(out).toHaveLength(40);
    expect(Array.from(out[33])).toEqual([1, 1, 0]);
    expect(fetch.calls).toEqual(["http://127.0.0.1:11434/api/embed", "http://127.0.0.1:11434/api/embed"]);
    expect(fetch.inits.map((i) => i.redirect)).toEqual(["error", "error"]);
    expect(e.model).toBe("nomic-embed-text");
  });

  it("refuses a non-loopback URL (and localhost) before sending anything", () => {
    const fetch = fakeFetch(() => ({ ok: true, status: 200, json: {} }));
    for (const url of ["https://api.example.com", "http://localhost:11434"]) {
      expect(() => makeOllamaEmbedder({ url, model: "m", fetch })).toThrow(SindriError);
    }
    expect(fetch.calls).toEqual([]);
  });

  it("refuses a cloud model name before sending anything", () => {
    const fetch = fakeFetch(() => ({ ok: true, status: 200, json: {} }));
    expect(() => makeOllamaEmbedder({ url: "http://127.0.0.1:11434", model: "embed-Cloud-large", fetch })).toThrow("cloud model");
    expect(fetch.calls).toEqual([]);
  });

  it("surfaces a refused redirect as an unreachable server", async () => {
    const redirecting: FetchLike = async (_u, init) => {
      if (init.redirect === "error") throw new Error("redirect mode is set to error");
      return { ok: true, status: 200, json: async () => ({ embeddings: [[1]] }) };
    };
    const e = makeOllamaEmbedder({ url: "http://127.0.0.1:11434", model: "m", fetch: redirecting });
    await expect(e.embed(["a"])).rejects.toThrow("embedding server unreachable: redirect mode is set to error");
  });

  it("reports a missing model, a down server and a malformed answer as SND-INDEX-006", async () => {
    const url = "http://127.0.0.1:11434";
    const missing = makeOllamaEmbedder({ url, model: "m", fetch: fakeFetch(() => ({ ok: false, status: 404, json: {} })) });
    await expect(missing.embed(["a"])).rejects.toThrow("embedding request failed (HTTP 404); is the model pulled? (sindri index setup)");
    const down = makeOllamaEmbedder({ url, model: "m", fetch: async () => { throw new Error("connect ECONNREFUSED"); } });
    await expect(down.embed(["a"])).rejects.toThrow("embedding server unreachable: connect ECONNREFUSED");
    const bad = makeOllamaEmbedder({ url, model: "m", fetch: fakeFetch(() => ({ ok: true, status: 200, json: { nope: 1 } })) });
    await expect(bad.embed(["a"])).rejects.toThrow("embedding server answered in an unexpected shape");
    expect(await bad.embed([])).toEqual([]);
  });

  it("aborts at the shorter of the caller's budget and its own timeout", async () => {
    const url = "http://127.0.0.1:11434";
    const t0 = Date.now();
    await expect(makeOllamaEmbedder({ url, model: "m", fetch: hang }).embed(["a"], { timeoutMs: 20 })).rejects.toThrow("embedding server unreachable: aborted");
    await expect(makeOllamaEmbedder({ url, model: "m", fetch: hang, timeoutMs: 20 }).embed(["a"], { timeoutMs: 5000 })).rejects.toThrow("aborted");
    expect(Date.now() - t0).toBeLessThan(2000);
  });

  it("encodes vectors, computes cosine and caps the embedded text", () => {
    const v = new Float32Array([1, 2, 3]);
    expect(Array.from(decodeVec(encodeVec(v)))).toEqual([1, 2, 3]);
    expect(cosine(new Float32Array([1, 0]), new Float32Array([1, 0]))).toBeCloseTo(1);
    expect(cosine(new Float32Array([1, 0]), new Float32Array([0, 1]))).toBeCloseTo(0);
    expect(cosine(new Float32Array([0, 0]), new Float32Array([1, 1]))).toBe(0);
    expect(embeddingText({ name: "f", signature: "(a)", body: "x".repeat(5000) })).toHaveLength(2000);
  });
});
