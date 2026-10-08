import { SindriError } from "../errors.js";
import { BUILTIN_PATTERNS, type ScrubPattern } from "./patterns.js";

export type { ScrubPattern } from "./patterns.js";

export interface ScrubHit {
  kind: string;
  start: number;
  end: number;
}

export interface Scrubber {
  find(text: string): ScrubHit[];
  scrub(text: string): { text: string; hits: ScrubHit[] };
  scrubDeep<T>(value: T): T;
}

function withFlags(re: RegExp): RegExp {
  const flags = new Set([...re.flags, "g", "d"]);
  return new RegExp(re.source, [...flags].join(""));
}

const PLACEHOLDER = /\[REDACTED:[A-Za-z0-9_.-]{1,64}\]/g;

export function makeScrubber(extra: readonly ScrubPattern[] = []): Scrubber {
  const patterns = [...BUILTIN_PATTERNS, ...extra].map((p, order) => ({ ...p, re: withFlags(p.re), order }));

  function find(text: string): ScrubHit[] {
    // Scrubbing is idempotent: a hit that lies inside one of our own
    // [REDACTED:kind] placeholders is not a secret.
    const placeholders = [...text.matchAll(PLACEHOLDER)].map((m) => [m.index as number, (m.index as number) + m[0].length]);
    const inPlaceholder = (start: number, end: number): boolean => placeholders.some(([a, b]) => start >= a && end <= b);
    const raw: (ScrubHit & { order: number })[] = [];
    for (const p of patterns) {
      for (const m of text.matchAll(p.re)) {
        const start = m.index as number; // typed number | undefined before TS 5.9 lib typings
        const group = p.valueGroup === undefined ? undefined : m.indices?.[p.valueGroup];
        const span = group ?? [start, start + m[0].length];
        if (!inPlaceholder(span[0], span[1])) raw.push({ kind: p.kind, start: span[0], end: span[1], order: p.order });
      }
    }
    raw.sort((a, b) => a.start - b.start || a.order - b.order);
    const merged: ScrubHit[] = [];
    for (const h of raw) {
      const last = merged.at(-1);
      if (last !== undefined && h.start < last.end) {
        last.end = Math.max(last.end, h.end);
      } else {
        merged.push({ kind: h.kind, start: h.start, end: h.end });
      }
    }
    return merged;
  }

  function scrub(text: string): { text: string; hits: ScrubHit[] } {
    const hits = find(text);
    let out = text;
    for (const h of [...hits].reverse()) out = `${out.slice(0, h.start)}[REDACTED:${h.kind}]${out.slice(h.end)}`;
    return { text: out, hits };
  }

  function scrubDeep<T>(value: T): T {
    const walk = (v: unknown): unknown => {
      if (typeof v === "string") return scrub(v).text;
      if (Array.isArray(v)) return v.map(walk);
      if (v !== null && typeof v === "object") {
        return Object.fromEntries(Object.entries(v).map(([k, x]) => [scrub(k).text, walk(x)]));
      }
      return v;
    };
    return walk(value) as T;
  }

  return { find, scrub, scrubDeep };
}

// A quantified group that itself contains a quantifier, e.g. (a+)+ or (\w*)*,
// can backtrack catastrophically. Profile patterns run on every record, so refuse them.
const NESTED_QUANTIFIER = /\((?:[^()\\]|\\.)*[+*}](?:[^()\\]|\\.)*\)[+*{]/;
// A quantified group with alternatives, e.g. (a|aa)+, can backtrack the same way.
const QUANTIFIED_ALTERNATION = /\((?:[^()\\]|\\.)*\|(?:[^()\\]|\\.)*\)[+*{]/;

export function compileExtraPatterns(specs: readonly { kind: string; regex: string }[]): ScrubPattern[] {
  return specs.map((spec, i) => {
    if (NESTED_QUANTIFIER.test(spec.regex) || QUANTIFIED_ALTERNATION.test(spec.regex)) {
      throw new SindriError("SND-SCRUB-001", `scrub.extraPatterns[${i}] has a nested quantifier, which can hang the scrubber`, {
        fix: "rewrite it without a quantified group that contains a quantifier",
      });
    }
    try {
      const re = new RegExp(spec.regex, "g");
      if (re.test("")) {
        throw new SindriError("SND-SCRUB-001", `scrub.extraPatterns[${i}] matches the empty string`, { fix: "make the pattern require at least one character" });
      }
      re.lastIndex = 0;
      return { kind: spec.kind, re };
    } catch (e) {
      if (e instanceof SindriError) throw e;
      throw new SindriError("SND-SCRUB-001", `scrub.extraPatterns[${i}] does not compile: ${(e as Error).message}`);
    }
  });
}
