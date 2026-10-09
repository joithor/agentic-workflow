import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import type { Source, SourceQuery, SourceRecord } from "../../src/scope/source.js";

// How a source treats `asOf` (spec amendment 4):
// - "filters": only records created at or before asOf come back (transcripts, Linear);
// - "excludes": a backtest gets nothing, because the source can't date its records (notes, code);
// - "ignores": the source is the subject itself, not evidence, so asOf does not apply (the brief file).
export type AsOfMode = "filters" | "excludes" | "ignores";

export interface SourceFixture {
  source: Source;
  // At least two records match these keywords (one for a single-record source), and every
  // matching record's raw text carries SECRET and HIDDEN.
  keywords: string[];
  asOf: { mode: AsOfMode; at: Date };
  // Directories the source reads; nothing in them may change across find().
  roots: string[];
  // Counts outside effects (for example network calls); must not change across find().
  effects?: () => number;
}

export const CONTRACT_SECRET = "AKIA" + "QRSTUVWXYZ234567";
export const CONTRACT_HIDDEN = "<!-- contract-hidden-marker -->";

function snapshot(roots: string[]): string[] {
  const out: string[] = [];
  const walk = (dir: string): void => {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries.sort((a, b) => a.name.localeCompare(b.name))) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) walk(p);
      else {
        const st = fs.statSync(p);
        out.push(`${p} ${st.size} ${st.mtimeMs}`);
      }
    }
  };
  for (const r of roots) walk(r);
  return out;
}

async function found(source: Source, q: SourceQuery): Promise<SourceRecord[]> {
  const r = await source.find(q);
  if (!r.ok) throw new Error(`${source.name}: ${r.error.message}`);
  return r.value;
}

// Every scope Source must pass this (spec §11.2): find respects limit and asOf, every record has a
// stable, unique ref, its text is stripped and scrubbed at fetch, and find writes nothing.
export function sourceContractTests(name: string, make: () => Promise<SourceFixture>): void {
  describe(`Source contract: ${name}`, () => {
    it("respects limit", async () => {
      const f = await make();
      const all = await found(f.source, { keywords: f.keywords, asOf: null, limit: 50 });
      expect(all.length).toBeGreaterThan(0);
      expect((await found(f.source, { keywords: f.keywords, asOf: null, limit: 1 })).length).toBeLessThanOrEqual(1);
    });

    it("respects asOf", async () => {
      const f = await make();
      const all = await found(f.source, { keywords: f.keywords, asOf: null, limit: 50 });
      const asOf = await found(f.source, { keywords: f.keywords, asOf: f.asOf.at, limit: 50 });
      if (f.asOf.mode === "excludes") expect(asOf).toEqual([]);
      if (f.asOf.mode === "ignores") expect(asOf).toEqual(all);
      if (f.asOf.mode === "filters") {
        expect(asOf.length).toBeGreaterThan(0);
        expect(asOf.length).toBeLessThan(all.length);
        for (const r of asOf) expect(r.createdAt !== null && Date.parse(r.createdAt) <= f.asOf.at.getTime()).toBe(true);
      }
    });

    it("gives every record a stable, unique ref with a source prefix", async () => {
      const f = await make();
      const q = { keywords: f.keywords, asOf: null, limit: 50 };
      const refs = (await found(f.source, q)).map((r) => r.ref);
      expect(new Set(refs).size).toBe(refs.length);
      for (const ref of refs) expect(ref).toMatch(/^[a-z-]+:\S+$/);
      expect((await found(f.source, q)).map((r) => r.ref)).toEqual(refs);
    });

    it("strips and scrubs every text at fetch", async () => {
      const f = await make();
      const all = await found(f.source, { keywords: f.keywords, asOf: null, limit: 50 });
      for (const r of all) {
        for (const t of [r.title, r.text, r.author ?? ""]) {
          expect(t).not.toContain(CONTRACT_SECRET);
          expect(t).not.toContain("contract-hidden-marker");
          expect(t).not.toContain("<!--");
        }
      }
      expect(all.some((r) => r.text.includes("[REDACTED:"))).toBe(true);
    });

    it("writes nothing", async () => {
      const f = await make();
      const before = snapshot(f.roots);
      const effects = f.effects?.() ?? 0;
      await found(f.source, { keywords: f.keywords, asOf: null, limit: 50 });
      await found(f.source, { keywords: f.keywords, asOf: f.asOf.at, limit: 50 });
      expect(snapshot(f.roots)).toEqual(before);
      expect(f.effects?.() ?? 0).toBe(effects);
    });
  });
}
