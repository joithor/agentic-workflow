import fs from "node:fs";
import path from "node:path";

import { z } from "zod";

import { discoverFiles } from "../transcript/discover.js";
import type { HumanTurn } from "./human-turns.js";
import { extractHumanTurns } from "./human-turns.js";
import type { ItemRecord, Size } from "./items.js";
import { autoStartShare, ItemRecordSchema } from "./items.js";
import type { PatternName } from "./patterns.js";
import { countPatterns } from "./patterns.js";
import { itemIdsForSession, sessionTokenTotals, summarizeItemUsage } from "./usage.js";

export interface AuditSummary {
  sessions: number;
  turns: number;
  commands: number;
  interrupts: number;
  duplicates: number;
  patterns: Record<PatternName, { turns: number; sessions: number }>;
  usage: { items: number; medianTokens: number; p75Tokens: number };
  autoStart: { eligible: number; total: number; share: number } | null;
}

function readItems(file: string): ItemRecord[] {
  const parsed = z.array(ItemRecordSchema).safeParse(JSON.parse(fs.readFileSync(file, "utf8")));
  if (!parsed.success) throw new Error(`items file ${file} is invalid: ${parsed.error.issues[0].message}`);
  return parsed.data;
}

export async function runAudit(opts: { projectsDir: string; since: Date; outDir: string; itemPattern: RegExp; itemsFile: string | null; maxSize: Size }): Promise<AuditSummary> {
  const items = opts.itemsFile === null ? null : readItems(opts.itemsFile);
  fs.mkdirSync(opts.outDir, { recursive: true, mode: 0o700 });
  // Verbatim human turns can hold pasted secrets: owner-only, like every file in the audit directory.
  const turnsOut = fs.createWriteStream(path.join(opts.outDir, "human-turns.jsonl"), { mode: 0o600 });
  const all: HumanTurn[] = [];
  const perSession: { session: string; items: string[]; total: number }[] = [];
  let sessions = 0;
  // Resume and fork copy earlier human turns into the new transcript with the same timestamp. One Set
  // across all files; the copy lands in whichever file is read second (discoverFiles order), which does
  // not change the counts. Turns with an empty ts are never deduped.
  const seen = new Set<string>();
  let duplicates = 0;
  for (const file of discoverFiles(opts.projectsDir)) {
    if (!file.isMain) continue;
    if (fs.statSync(file.path).mtime < opts.since) continue;
    const turns: HumanTurn[] = [];
    for await (const t of extractHumanTurns({ path: file.path, project: file.project, sessionId: file.sessionId })) {
      if (t.ts !== "" && new Date(t.ts) < opts.since) continue;
      if (t.ts !== "") {
        const key = `${t.ts}\u0000${t.text}`;
        if (seen.has(key)) {
          duplicates += 1;
          continue;
        }
        seen.add(key);
      }
      turns.push(t);
      turnsOut.write(`${JSON.stringify(t)}\n`);
    }
    if (turns.length === 0) continue;
    sessions += 1;
    all.push(...turns);
    const tok = await sessionTokenTotals(file.path);
    perSession.push({ session: file.sessionId, items: itemIdsForSession(turns, opts.itemPattern), total: tok.input + tok.cacheRead + tok.cacheCreation + tok.output });
  }
  await new Promise<void>((resolve) => turnsOut.end(resolve));
  const usage = summarizeItemUsage(perSession);
  const summary: AuditSummary = {
    sessions,
    turns: all.filter((t) => t.kind === "turn").length,
    commands: all.filter((t) => t.kind === "command").length,
    interrupts: all.filter((t) => t.kind === "interrupt").length,
    duplicates,
    patterns: countPatterns(all),
    usage: { items: usage.items, medianTokens: usage.medianTokens, p75Tokens: usage.p75Tokens },
    autoStart: items === null ? null : autoStartShare(items, opts.maxSize),
  };
  fs.writeFileSync(path.join(opts.outDir, "summary.json"), `${JSON.stringify(summary, null, 2)}\n`);
  fs.writeFileSync(path.join(opts.outDir, "baseline.md"), renderBaseline(summary, opts));
  return summary;
}

function renderBaseline(s: AuditSummary, opts: { since: Date; maxSize: Size }): string {
  const rows = (Object.entries(s.patterns) as [PatternName, { turns: number; sessions: number }][])
    .map(([n, v]) => `| ${n} | ${v.turns} | ${v.sessions} |`)
    .join("\n");
  const pct = (n: number): string => (s.turns === 0 ? "0.0" : ((100 * n) / s.turns).toFixed(1));
  const auto = s.autoStart === null ? "_no items file given_" : `${s.autoStart.eligible}/${s.autoStart.total} (${(100 * s.autoStart.share).toFixed(1)}%) at maxSize ${opts.maxSize}`;
  return [
    `# Transcript audit baseline`,
    ``,
    `Since ${opts.since.toISOString()}: ${s.sessions} sessions, ${s.turns} typed turns, ${s.commands} slash commands, ${s.interrupts} interrupts, ${s.duplicates} copied turns skipped (resumed or forked sessions, deduped by timestamp and text).`,
    ``,
    `| pattern | turns | sessions |`,
    `|---|---|---|`,
    rows,
    ``,
    `Shares of typed turns: ${(Object.keys(s.patterns) as PatternName[]).map((n) => `${n} ${pct(s.patterns[n].turns)}%`).join(", ")}.`,
    ``,
    `Tokens per item: ${s.usage.items} items, median ${Math.round(s.usage.medianTokens)}, p75 ${Math.round(s.usage.p75Tokens)}.`,
    ``,
    `Auto-start share: ${auto}.`,
    ``,
  ].join("\n");
}
