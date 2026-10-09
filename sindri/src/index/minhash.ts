// MinHash + LSH over token shingles (spec §6.2 clones): near-duplicate candidates
// come from shared bands, never from comparing every pair.
export const SHINGLE = 5;
export const PERMS = 64;
export const BANDS = 16;
const ROWS = PERMS / BANDS;

export function shingles(tokens: string[], k: number = SHINGLE): Set<string> {
  if (tokens.length === 0) return new Set();
  if (tokens.length < k) return new Set([tokens.join(" ")]);
  const out = new Set<string>();
  for (let i = 0; i + k <= tokens.length; i++) out.add(tokens.slice(i, i + k).join(" "));
  return out;
}

// 32-bit FNV-1a with a per-permutation seed mixed into the offset basis.
function fnv1a(text: string, seed: number): number {
  let h = (0x811c9dc5 ^ Math.imul(seed + 1, 0x9e3779b1)) >>> 0;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h >>> 0;
}

export function signature(tokens: string[]): Uint32Array {
  const sig = new Uint32Array(PERMS).fill(0xffffffff);
  for (const s of shingles(tokens)) {
    for (let p = 0; p < PERMS; p++) {
      const h = fnv1a(s, p);
      if (h < sig[p]) sig[p] = h;
    }
  }
  return sig;
}

export function bandKeys(sig: Uint32Array): string[] {
  const keys: string[] = [];
  for (let b = 0; b < BANDS; b++) {
    const rows = Array.from(sig.subarray(b * ROWS, (b + 1) * ROWS), (v) => v.toString(16).padStart(8, "0"));
    keys.push(`${b}:${rows.join("")}`);
  }
  return keys;
}

export function estimateJaccard(a: Uint32Array, b: Uint32Array): number {
  let same = 0;
  for (let i = 0; i < PERMS; i++) if (a[i] === b[i]) same++;
  return same / PERMS;
}

export function jaccard(a: Set<string>, b: Set<string>): number {
  if (a.size === 0 && b.size === 0) return 0;
  let inter = 0;
  for (const x of a) if (b.has(x)) inter++;
  return inter / (a.size + b.size - inter);
}

export function encodeSig(sig: Uint32Array): Buffer {
  return Buffer.from(sig.buffer, sig.byteOffset, sig.byteLength);
}

export function decodeSig(buf: Buffer): Uint32Array {
  return new Uint32Array(buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength));
}
