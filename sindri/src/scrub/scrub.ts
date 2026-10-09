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

export const escapeRe = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

export function makeScrubber(extra: readonly ScrubPattern[] = []): Scrubber {
  const patterns = [...BUILTIN_PATTERNS, ...extra].map((p, order) => ({ ...p, re: withFlags(p.re), order }));
  // Only a placeholder naming one of this scrubber's kinds is skipped, so a crafted
  // "[REDACTED:<secret>]" is still scanned.
  const kinds = [...new Set(patterns.map((p) => p.kind))].map(escapeRe).join("|");
  const PLACEHOLDER = new RegExp(`\\[REDACTED:(?:${kinds})\\]`, "g");

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

// Patterns that can backtrack catastrophically. Profile patterns run on every
// record, so refuse them. The walk follows nested groups at any depth:
// - a repeated group whose content holds a quantifier, e.g. (a+)+ or ((a+))+
// - a repeated group with alternatives, e.g. (a|aa)+
// - two unbounded quantifiers in a row, e.g. \w+\w+! (polynomial backtracking)
// A syntactic check can't be complete; it refuses the known shapes.
export function regexRisk(src: string): string | null {
  let i = 0;
  let risk: string | null = null;
  // "none", "once" (? or {0,1} / {1}), "bounded" ({n,m}), or "unbounded" (+ * {n,}).
  const quantifier = (): "none" | "once" | "bounded" | "unbounded" => {
    let q: "none" | "once" | "bounded" | "unbounded" = "none";
    if (src[i] === "+" || src[i] === "*") {
      q = "unbounded";
      i++;
    } else if (src[i] === "?") {
      q = "once";
      i++;
    } else {
      const m = /^\{(\d+)(,(\d*))?\}/.exec(src.slice(i));
      if (m === null) return "none";
      i += m[0].length;
      const max = m[2] === undefined ? Number(m[1]) : m[3] === "" ? Infinity : Number(m[3]);
      q = max === Infinity ? "unbounded" : max > 1 ? "bounded" : "once";
    }
    if (src[i] === "?") i++; // lazy
    return q;
  };
  const sequence = (): { quantified: boolean; alternation: boolean } => {
    let quantified = false;
    let alternation = false;
    let prevUnbounded = false;
    while (i < src.length && src[i] !== ")") {
      if (src[i] === "|") {
        alternation = true;
        prevUnbounded = false;
        i++;
        continue;
      }
      let inner = { quantified: false, alternation: false };
      if (src[i] === "\\") {
        i += 2;
      } else if (src[i] === "[") {
        i++;
        if (src[i] === "^") i++;
        if (src[i] === "]") i++;
        while (i < src.length && src[i] !== "]") i += src[i] === "\\" ? 2 : 1;
        i++;
      } else if (src[i] === "(") {
        i++;
        if (src[i] === "?") {
          i++;
          if (src[i] === "<" && src[i + 1] !== "=" && src[i + 1] !== "!") {
            while (i < src.length && src[i] !== ">") i++;
          } else if (src[i] === "<") {
            i++;
          }
          i++;
        }
        inner = sequence();
        i++;
      } else {
        i++;
      }
      const q = quantifier();
      const repeats = q === "bounded" || q === "unbounded";
      if (repeats && inner.quantified) risk ??= "a nested quantifier";
      if (repeats && inner.alternation) risk ??= "a quantified alternation";
      if (q === "unbounded" && prevUnbounded) risk ??= "two unbounded quantifiers in a row";
      prevUnbounded = q === "unbounded";
      quantified ||= q !== "none" || inner.quantified;
      alternation ||= !repeats && inner.alternation;
    }
    return { quantified, alternation };
  };
  while (i < src.length) {
    sequence();
    i++; // a stray ")": the RegExp compile reports it
  }
  return risk;
}

export function compileExtraPatterns(specs: readonly { kind: string; regex: string }[]): ScrubPattern[] {
  return specs.map((spec, i) => {
    const risk = regexRisk(spec.regex);
    if (risk !== null) {
      throw new SindriError("SND-SCRUB-001", `scrub.extraPatterns[${i}] has ${risk}, which can hang the scrubber`, {
        fix: "rewrite it without a repeated group that holds a quantifier or |, and bound one of any two adjacent + or * (e.g. {1,64})",
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
