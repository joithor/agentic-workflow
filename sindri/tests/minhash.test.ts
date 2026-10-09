import { describe, expect, it } from "vitest";

import { bandKeys, decodeSig, encodeSig, estimateJaccard, jaccard, PERMS, shingles, signature } from "../src/index/minhash.js";

const seq = (n: number, tag = "a") => Array.from({ length: n }, (_, i) => `${tag}${i % 17}-${i}`);

describe("minhash", () => {
  it("builds k-token shingles, and one shingle for short sequences", () => {
    expect([...shingles(["a", "b", "c", "d", "e", "f"], 5)]).toEqual(["a b c d e", "b c d e f"]);
    expect([...shingles(["a", "b"], 5)]).toEqual(["a b"]);
    expect(shingles([], 5).size).toBe(0);
  });

  it("is deterministic and estimates Jaccard closely", () => {
    const a = seq(200);
    const b = [...seq(180), ...seq(20, "z")];
    expect(signature(a)).toEqual(signature(a));
    expect(signature(a)).toHaveLength(PERMS);
    const exact = jaccard(shingles(a), shingles(b));
    expect(Math.abs(estimateJaccard(signature(a), signature(b)) - exact)).toBeLessThan(0.15);
    expect(estimateJaccard(signature(a), signature(a))).toBe(1);
    expect(estimateJaccard(signature(seq(100)), signature(seq(100, "q")))).toBeLessThan(0.1);
    expect(jaccard(new Set(), new Set())).toBe(0);
  });

  it("puts near-duplicates in a shared band and unrelated code in none", () => {
    const a = seq(300);
    const near = [...seq(295), "x1", "x2", "x3", "x4", "x5"];
    const share = (x: string[], y: string[]) => bandKeys(signature(x)).some((k) => bandKeys(signature(y)).includes(k));
    expect(share(a, near)).toBe(true);
    expect(share(a, seq(300, "other"))).toBe(false);
    expect(bandKeys(signature(a))).toHaveLength(16);
    expect(bandKeys(signature(a))[0]).toMatch(/^0:[0-9a-f]{32}$/);
  });

  it("round-trips signatures through blobs", () => {
    const sig = signature(seq(50));
    expect(decodeSig(encodeSig(sig))).toEqual(sig);
  });
});
