import { allSymbols, bandCandidates, depRows, embeddingRows, layers, symbolsByAstHash, type IndexDb, type Layer, type SymbolRow } from "./db.js";
import { readManifestDeps } from "./deps-layer.js";
import { cosine, decodeVec, embeddingText, type Embedder } from "./embed.js";
import { bandKeys, estimateJaccard } from "./minhash.js";
import type { Overlay } from "./overlay.js";

export const SIGNAL_TYPES = [
  "reinvented:exact", "reinvented:name", "reinvented:embedding", "reinvented:graph", "reinvented:dependency",
  "generalize:near-clone", "simpler:diff-size", "simpler:complexity", "simpler:exports",
] as const;
export type SignalType = (typeof SIGNAL_TYPES)[number];

export interface Signal {
  type: SignalType;
  layer: Layer;
  value: number;
  threshold: number;
  at: string;
  existing: string | null;
  detail: string;
  name: string | null;
  astHash: string | null;
}

export interface Thresholds {
  nameSimilarity: number;
  embedding: number;
  embeddingAst: number;
  nearCloneTokens: number;
  nearCloneJaccard: number;
  callOverlap: number;
  complexityDelta: number;
}

const MIN_TOKENS = 20;
const MIN_CALLS = 3;
// Names come from the repo, not from Sindri: fence them and escape what could close the fence.
const u = (s: string): string => `<untrusted>${s.slice(0, 120).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")}</untrusted>`;
const loc = (s: { file: string; startLine: number }): string => `${s.file}:${s.startLine}`;

function words(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_\-.]+/g, " ").toLowerCase().trim();
}

function levenshtein(a: string, b: string): number {
  const prev = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    let diag = prev[0];
    prev[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const up = prev[j];
      prev[j] = Math.min(prev[j] + 1, prev[j - 1] + 1, diag + (a[i - 1] === b[j - 1] ? 0 : 1));
      diag = up;
    }
  }
  return prev[b.length];
}

export function nameSimilarity(a: string, b: string): number {
  const x = words(a);
  const y = words(b);
  const max = Math.max(x.length, y.length);
  return max === 0 ? 1 : 1 - levenshtein(x, y) / max;
}

// Callers pass at least MIN_CALLS names, so the union is never empty.
function setJaccard(a: string[], b: string[]): number {
  const A = new Set(a);
  const B = new Set(b);
  const inter = [...A].filter((x) => B.has(x)).length;
  const union = new Set([...A, ...B]).size;
  return inter / union;
}

export async function computeSignals(i: {
  base: IndexDb;
  overlay: Overlay;
  t: Thresholds;
  sizeBudget: number;
  exportAllowance: number;
  embed: { embedder: Embedder; deadline: number; now: () => number } | null;
}): Promise<{ signals: Signal[]; deferred: Layer[] }> {
  const { base, overlay, t } = i;
  const signals: Signal[] = [];
  const deferred: Layer[] = [];
  const baseAll = allSymbols(base);
  // Symbols of files this commit doesn't touch (a renamed file's old path counts as touched).
  const stable = baseAll.filter((s) => !overlay.changedPaths.has(s.file));
  const unchanged = (s: SymbolRow): boolean => !overlay.changedPaths.has(s.file);
  const reusable = stable.filter((s) => s.kind !== "class" && (s.exported || s.utility));
  const before = new Map(baseAll.map((s) => [`${s.file}#${s.name}`, s]));
  const fresh = overlay.symbols.filter((s) => before.get(`${s.file}#${s.name}`)?.astHash !== s.astHash);
  const candidates = fresh.filter((s) => s.kind !== "class" && s.tokens.length >= MIN_TOKENS);

  for (const s of candidates) {
    const exact = symbolsByAstHash(base, s.astHash).find((b) => unchanged(b) && b.name !== s.name);
    if (exact !== undefined) {
      signals.push({ type: "reinvented:exact", layer: "clones", value: 1, threshold: 1, at: loc(s), existing: loc(exact), detail: `${u(s.name)} has the same structure as ${u(exact.name)}`, name: s.name, astHash: s.astHash });
      continue;
    }
    const ids = new Set(bandCandidates(base, bandKeys(s.minhash)));
    const near = stable
      .filter((b) => ids.has(b.id) && b.tokenCount >= t.nearCloneTokens)
      .map((b) => ({ b, j: estimateJaccard(s.minhash, b.minhash) }))
      .sort((x, y) => y.j - x.j)[0];
    if (near !== undefined && s.tokens.length >= t.nearCloneTokens && near.j >= t.nearCloneJaccard) {
      signals.push({ type: "generalize:near-clone", layer: "clones", value: near.j, threshold: t.nearCloneJaccard, at: loc(s), existing: loc(near.b), detail: `${u(s.name)} is a near-copy of ${u(near.b.name)}; generalize at the second case`, name: s.name, astHash: s.astHash });
    }
    const named = reusable.find((b) => nameSimilarity(s.name, b.name) >= t.nameSimilarity && nameSimilarity(s.signature, b.signature) >= t.nameSimilarity);
    if (named !== undefined) {
      signals.push({ type: "reinvented:name", layer: "structure", value: nameSimilarity(s.name, named.name), threshold: t.nameSimilarity, at: loc(s), existing: loc(named), detail: `${u(s.name)} looks like ${u(named.name)}`, name: s.name, astHash: s.astHash });
    }
    const calls = s.callees.length >= MIN_CALLS ? reusable.find((b) => b.callees.length >= MIN_CALLS && setJaccard(s.callees, b.callees) >= t.callOverlap) : undefined;
    if (calls !== undefined) {
      signals.push({ type: "reinvented:graph", layer: "graph", value: setJaccard(s.callees, calls.callees), threshold: t.callOverlap, at: loc(s), existing: loc(calls), detail: `${u(s.name)} calls what ${u(calls.name)} calls`, name: s.name, astHash: s.astHash });
    }
  }

  if (i.embed !== null && candidates.length > 0) {
    const ready = layers(base).some((l) => l.layer === "embeddings" && l.status === "ok");
    const left = i.embed.deadline - i.embed.now();
    let vectors: Float32Array[] | null = null;
    if (ready && left > 0) {
      // The request is aborted at the remaining budget, and the race timer never outlives the call.
      let timer: ReturnType<typeof setTimeout> | undefined;
      const timeout = new Promise<null>((resolve) => {
        timer = setTimeout(() => resolve(null), left);
      });
      try {
        vectors = await Promise.race([
          i.embed.embedder.embed(candidates.map((s) => embeddingText({ name: s.name, signature: s.signature, body: s.text })), { timeoutMs: left }).catch(() => null),
          timeout,
        ]);
      } finally {
        clearTimeout(timer);
      }
    }
    if (vectors === null) {
      deferred.push("embeddings");
    } else {
      const found: Float32Array[] = vectors;
      const vecs = new Map(embeddingRows(base, i.embed.embedder.model).map((r) => [r.symbolId, decodeVec(r.vector)]));
      candidates.forEach((s, k) => {
        const best = stable
          .flatMap((b) => {
            const v = vecs.get(b.id);
            return v === undefined ? [] : [{ b, c: cosine(found[k], v) }];
          })
          .filter(({ b, c }) => c >= t.embedding && estimateJaccard(s.minhash, b.minhash) >= t.embeddingAst)
          .sort((x, y) => y.c - x.c)[0];
        if (best !== undefined) {
          signals.push({ type: "reinvented:embedding", layer: "embeddings", value: best.c, threshold: t.embedding, at: loc(s), existing: loc(best.b), detail: `${u(s.name)} means what ${u(best.b.name)} means`, name: s.name, astHash: s.astHash });
        }
      });
    }
  }

  const existingDeps = depRows(base);
  for (const m of overlay.manifests) {
    const known = new Set(existingDeps.filter((d) => d.manifest === m.path).map((d) => d.name));
    for (const added of readManifestDeps(m.path, m.text).filter((d) => !known.has(d.name))) {
      const twin = existingDeps.find((d) => d.name !== added.name && d.tags.some((tag) => added.tags.includes(tag)));
      if (twin !== undefined) {
        const tag = twin.tags.find((x) => added.tags.includes(x)) as string;
        signals.push({ type: "reinvented:dependency", layer: "deps", value: 1, threshold: 1, at: m.path, existing: twin.manifest, detail: `adds ${u(added.name)} (${tag}) while ${u(twin.name)} (${tag}) is already a dependency`, name: added.name, astHash: null });
      }
    }
  }

  if (overlay.addedLines > i.sizeBudget) {
    signals.push({ type: "simpler:diff-size", layer: "structure", value: overlay.addedLines, threshold: i.sizeBudget, at: "(diff)", existing: null, detail: `${overlay.addedLines} added lines; the size budget is ${i.sizeBudget}`, name: null, astHash: null });
  }
  for (const s of fresh) {
    const old = before.get(`${s.file}#${s.name}`);
    if (old !== undefined && s.complexity - old.complexity > t.complexityDelta) {
      signals.push({ type: "simpler:complexity", layer: "structure", value: s.complexity - old.complexity, threshold: t.complexityDelta, at: loc(s), existing: loc(old), detail: `${u(s.name)} grew from complexity ${old.complexity} to ${s.complexity}`, name: s.name, astHash: s.astHash });
    }
  }
  const newExports = fresh.filter((s) => s.exported && !before.has(`${s.file}#${s.name}`)).length;
  if (newExports > i.exportAllowance) {
    signals.push({ type: "simpler:exports", layer: "structure", value: newExports, threshold: i.exportAllowance, at: "(diff)", existing: null, detail: `${newExports} new exports; the allowance is ${i.exportAllowance}`, name: null, astHash: null });
  }
  return { signals, deferred };
}
