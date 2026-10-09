import { z } from "zod";

import { SindriError } from "../errors.js";
import type { FetchLike } from "./io.js";
import { isLoopbackUrl } from "./loopback.js";

export interface Embedder {
  model: string;
  embed(texts: string[], o?: { timeoutMs?: number }): Promise<Float32Array[]>;
}

const BATCH = 32;
const TEXT_CAP = 2000;
const Answer = z.object({ embeddings: z.array(z.array(z.number())) });

// Spec §6.2 offline guarantee: code goes only to a model on this machine, and a
// redirect from that endpoint is an error, not something to follow.
export function makeOllamaEmbedder(o: { url: string; model: string; fetch: FetchLike; timeoutMs?: number }): Embedder {
  if (!isLoopbackUrl(o.url)) throw new SindriError("SND-INDEX-005", `embedding URL ${o.url} is not loopback; the index never sends code off the machine`);
  if (/cloud/i.test(o.model)) throw new SindriError("SND-INDEX-005", `embedding model ${o.model} is a cloud model; the index never sends code off the machine`);
  const endpoint = `${o.url.replace(/\/+$/, "")}/api/embed`;
  const cap = o.timeoutMs ?? 30_000;
  return {
    model: o.model,
    async embed(texts, call) {
      const timeoutMs = Math.min(call?.timeoutMs ?? cap, cap);
      const out: Float32Array[] = [];
      for (let i = 0; i < texts.length; i += BATCH) {
        const input = texts.slice(i, i + BATCH);
        let res: Awaited<ReturnType<FetchLike>>;
        try {
          res = await o.fetch(endpoint, {
            method: "POST",
            headers: { "content-type": "application/json" },
            body: JSON.stringify({ model: o.model, input }),
            signal: AbortSignal.timeout(timeoutMs),
            redirect: "error",
          });
        } catch (e) {
          throw new SindriError("SND-INDEX-006", `embedding server unreachable: ${(e as Error).message}`);
        }
        if (!res.ok) throw new SindriError("SND-INDEX-006", `embedding request failed (HTTP ${res.status}); is the model pulled? (sindri index setup)`);
        const parsed = Answer.safeParse(await res.json());
        if (!parsed.success || parsed.data.embeddings.length !== input.length) {
          throw new SindriError("SND-INDEX-006", "embedding server answered in an unexpected shape");
        }
        out.push(...parsed.data.embeddings.map((v) => Float32Array.from(v)));
      }
      return out;
    },
  };
}

export function embeddingText(s: { name: string; signature: string; body: string }): string {
  return `${s.name}${s.signature}\n${s.body}`.slice(0, TEXT_CAP);
}

export function encodeVec(v: Float32Array): Buffer {
  return Buffer.from(v.buffer, v.byteOffset, v.byteLength);
}

export function decodeVec(b: Buffer): Float32Array {
  return new Float32Array(b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength));
}

export function cosine(a: Float32Array, b: Float32Array): number {
  let dot = 0;
  let na = 0;
  let nb = 0;
  for (let i = 0; i < a.length; i++) {
    dot += a[i] * b[i];
    na += a[i] * a[i];
    nb += b[i] * b[i];
  }
  return na === 0 || nb === 0 ? 0 : dot / Math.sqrt(na * nb);
}
