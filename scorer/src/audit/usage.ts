import fs from "node:fs";
import readline from "node:readline";

import type { HumanTurn } from "./human-turns.js";

export async function sessionTokenTotals(path: string): Promise<{ input: number; cacheRead: number; cacheCreation: number; output: number }> {
  const seen = new Set<string>();
  const sum = { input: 0, cacheRead: 0, cacheCreation: 0, output: 0 };
  const rl = readline.createInterface({ input: fs.createReadStream(path, { encoding: "utf8" }), crlfDelay: Infinity });
  for await (const line of rl) {
    let rec: { type?: unknown; message?: { id?: unknown; usage?: Record<string, unknown> } };
    try {
      const parsed: unknown = JSON.parse(line);
      if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) continue;
      rec = parsed as typeof rec;
    } catch {
      continue;
    }
    if (rec.type !== "assistant" || typeof rec.message?.id !== "string" || seen.has(rec.message.id)) continue;
    seen.add(rec.message.id);
    const u = rec.message.usage ?? {};
    const n = (k: string): number => (typeof u[k] === "number" ? (u[k] as number) : 0);
    sum.input += n("input_tokens");
    sum.cacheRead += n("cache_read_input_tokens");
    sum.cacheCreation += n("cache_creation_input_tokens");
    sum.output += n("output_tokens");
  }
  return sum;
}

export function itemIdsForSession(turns: readonly HumanTurn[], pattern: RegExp): string[] {
  const g = new RegExp(pattern.source, pattern.flags.includes("g") ? pattern.flags : `${pattern.flags}g`);
  const out: string[] = [];
  for (const t of turns) for (const m of t.text.matchAll(g)) if (!out.includes(m[0])) out.push(m[0]);
  return out;
}

function quantile(sorted: readonly number[], q: number): number {
  if (sorted.length === 0) return 0;
  const i = Math.min(sorted.length - 1, Math.floor(q * (sorted.length - 1) + 0.5));
  return sorted[i];
}

export function summarizeItemUsage(perSession: { session: string; items: string[]; total: number }[]): { items: number; medianTokens: number; p75Tokens: number; byItem: Record<string, number> } {
  const byItem: Record<string, number> = {};
  for (const s of perSession) {
    if (s.items.length === 0) continue;
    const share = s.total / s.items.length;
    for (const id of s.items) byItem[id] = (byItem[id] ?? 0) + share;
  }
  const values = Object.values(byItem).sort((a, b) => a - b);
  return { items: values.length, medianTokens: quantile(values, 0.5), p75Tokens: quantile(values, 0.75), byItem };
}
