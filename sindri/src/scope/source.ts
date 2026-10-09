import path from "node:path";

import type { Result } from "../adapters/types.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";

export interface SourceRecord {
  ref: string;
  kind: "brief" | "doc" | "note" | "transcript" | "issue" | "comment" | "code";
  title: string;
  text: string;
  author: string | null;
  createdAt: string | null;
  trust: "trusted" | "untrusted";
}

export interface SourceQuery {
  keywords: string[];
  asOf: Date | null;
  limit: number;
}

export interface Source {
  name: string;
  find(q: SourceQuery): Promise<Result<SourceRecord[]>>;
}

const STOP = new Set([
  "about", "after", "also", "been", "being", "from", "have", "into", "just", "like", "more", "most", "must", "need", "needs", "only",
  "other", "over", "same", "should", "some", "such", "than", "that", "their", "them", "then", "there", "these", "they", "this", "those",
  "through", "under", "very", "want", "were", "what", "when", "where", "which", "while", "will", "with", "without", "would", "your",
]);

export function keywordsOf(text: string, n = 20): string[] {
  const counts = new Map<string, number>();
  for (const w of text.toLowerCase().match(/[a-z][a-z0-9]{3,}/g) ?? []) {
    if (!STOP.has(w)) counts.set(w, (counts.get(w) ?? 0) + 1);
  }
  return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, n).map(([w]) => w);
}

export function keywordHits(text: string, keywords: string[]): number {
  const lower = text.toLowerCase();
  return keywords.filter((k) => lower.includes(k)).length;
}

// Spec §8.3: ingest strips HTML comments, zero-width characters, encoded blobs
// and remote image URLs. Remote links keep only their text. Control characters
// go too (a terminal escape in a Linear title must not reach a terminal).
// Invisible characters are written as \u escapes so they stay visible in review.
export function sanitizeIngest(text: string): string {
  return text
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/<img\b[^>]*>/gi, "")
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F\u200B-\u200F\u202A-\u202E\u2060-\u2064\u2066-\u2069\uFEFF]/g, "")
    .replace(/[A-Za-z0-9+\/=_-]{201,}/g, "[blob]")
    .replace(/!\[([^\]]*)\]\(https?:\/\/[^)\s]*\)/gi, "$1")
    .replace(/\[([^\]]*)\]\(https?:\/\/[^)\s]*\)/gi, "$1");
}

const defaultScrubber = makeScrubber();

export function scrubText(text: string, scrubber: Scrubber = defaultScrubber): string {
  return scrubber.scrub(text).text;
}

// The one call every source makes on every text it returns. Callers pass the
// profile's scrubber (built-in plus extra patterns) so its patterns reach ingest.
export function clean(text: string, scrubber: Scrubber = defaultScrubber): string {
  return scrubText(sanitizeIngest(text), scrubber);
}

export function escapeMarkup(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

// A fenced data block for model input: the body can never close its own fence.
export function fence(kind: string, text: string): string {
  return `<untrusted kind="${kind}">${text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")}</untrusted>`;
}

// The reference as a person sees it: no directories from notes or transcripts.
export function displayRef(ref: string): string {
  const m = /^(notes|file|transcript):([^#]*)(#.*)?$/.exec(ref);
  return m === null ? ref : `${m[1]}:${path.posix.basename(m[2])}${m[3] ?? ""}`;
}

// Stable reference ids for citations (R1, R2, …). R1 is the brief. Every record
// is fenced as untrusted data when packed for a model (spec §8.3); escaping means
// source text can never close its own fence.
export class RefTable {
  private readonly byRef = new Map<string, string>();
  private readonly records = new Map<string, SourceRecord>();

  add(r: SourceRecord): string {
    const existing = this.byRef.get(r.ref);
    if (existing !== undefined) return existing;
    const id = `R${this.records.size + 1}`;
    this.byRef.set(r.ref, id);
    this.records.set(id, r);
    return id;
  }

  get(id: string): SourceRecord | undefined {
    return this.records.get(id);
  }

  ids(): string[] {
    return [...this.records.keys()];
  }

  entries(): [string, SourceRecord][] {
    return [...this.records.entries()];
  }

  pack(maxChars: number): string {
    const all = this.entries();
    const first = all[0];
    const briefCap = first === undefined ? 0 : Math.min(first[1].text.length, Math.floor(maxChars / 2));
    const per = Math.max(200, Math.floor((maxChars - briefCap) / Math.max(1, all.length - 1)));
    return all
      .map(([id, r], i) => {
        const cap = i === 0 ? briefCap : per;
        const text = r.text.length > cap ? `${r.text.slice(0, cap)} [trimmed]` : r.text;
        return `<untrusted id="${id}" kind="${r.kind}" ref="${escapeMarkup(r.ref)}" author="${escapeMarkup(r.author ?? "unknown")}">${escapeMarkup(text)}</untrusted>`;
      })
      .join("\n");
  }
}
