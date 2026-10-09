import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import { makeScrubber } from "../scrub/scrub.js";
import type { EvolveCtx } from "./ctx.js";

const scrubber = makeScrubber();

export interface Block {
  text: string;
  toolResult: boolean;
}

export interface SessionLine {
  file: string;
  session: string;
  n: number;
  ref: string;
  ts: string;
  type: string;
  branch: string;
  blocks: Block[];
  tools: string[]; // names of the entry's tool_use blocks (an assistant line that edited a file has "Edit", "Write", ...)
}

interface RawEntry {
  type?: unknown;
  timestamp?: unknown;
  cwd?: unknown;
  gitBranch?: unknown;
  content?: unknown;
  message?: { content?: unknown };
}

export function textBlocks(content: unknown): Block[] {
  if (typeof content === "string") return [{ text: content, toolResult: false }];
  if (!Array.isArray(content)) return [];
  return content.flatMap((b: unknown): Block[] => {
    if (typeof b !== "object" || b === null) return [];
    const blk = b as { type?: unknown; text?: unknown; content?: unknown };
    if (blk.type === "text" && typeof blk.text === "string") return [{ text: blk.text, toolResult: false }];
    if (blk.type === "tool_result") return [{ text: textBlocks(blk.content).map((x) => x.text).join("\n"), toolResult: true }];
    return [];
  });
}

export function toolNames(content: unknown): string[] {
  if (!Array.isArray(content)) return [];
  return content.flatMap((b: unknown): string[] => {
    if (typeof b !== "object" || b === null) return [];
    const blk = b as { type?: unknown; name?: unknown };
    return blk.type === "tool_use" && typeof blk.name === "string" ? [blk.name] : [];
  });
}

// transcript:<first 8 characters of the session file name>#<line>: no project directory names.
export const sessionOf = (file: string): string => path.basename(file, ".jsonl").replace(/[^A-Za-z0-9]/g, "").slice(0, 8);

const underRepo = (cwd: string, repo: string): boolean => cwd === repo || cwd.startsWith(`${repo}/`);

function parseEntry(raw: string | undefined): RawEntry | null {
  if (raw === undefined) return null;
  let v: unknown;
  try {
    v = JSON.parse(raw);
  } catch {
    return null;
  }
  return typeof v === "object" && v !== null ? (v as RawEntry) : null;
}

const contentOf = (e: RawEntry): unknown => e.message?.content ?? e.content;
const blocksOf = (e: RawEntry): Block[] => textBlocks(contentOf(e));

function walk(dir: string, out: string[]): void {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (e.isFile() && e.name.endsWith(".jsonl")) out.push(p);
  }
}

// Resumed and forked sessions copy earlier lines into the new file with the same timestamp. The key
// is (timestamp, whitespace-normalized text); a line without a timestamp or text has no key.
function dedupeKey(ts: string, blocks: Block[]): string | null {
  const text = blocks.map((b) => b.text).join("\n").replace(/\s+/g, " ").trim();
  return ts === "" || text === "" ? null : `${ts}\u0000${text}`;
}

// Only sessions of this repo (spec amendment 6): a line counts while the latest cwd seen in its
// file is the repo or under it. Files not modified since `since` aren't read. A non-assistant line
// already seen (same timestamp and text) in an earlier file of this scan is a copy and is skipped.
export function readRepoSessions(dir: string, repo: string, since: Date, maxFiles = 500): { lines: SessionLine[]; files: number; skipped: number } {
  if (!fs.existsSync(dir)) return { lines: [], files: 0, skipped: 0 };
  const all: string[] = [];
  walk(dir, all);
  const recent = all.map((f) => ({ f, m: fs.statSync(f).mtimeMs })).filter((x) => x.m >= since.getTime()).sort((a, b) => b.m - a.m).slice(0, maxFiles).map((x) => x.f).sort();
  const lines: SessionLine[] = [];
  const seen = new Set<string>(); // keys of lines in earlier files
  for (const file of recent) {
    let cwd = "";
    const own: string[] = []; // added to `seen` only after the file, so a file never dedupes against itself
    fs.readFileSync(file, "utf8").split("\n").forEach((raw, i) => {
      const e = parseEntry(raw);
      if (e === null) return;
      if (typeof e.cwd === "string") cwd = e.cwd;
      if (!underRepo(cwd, repo)) return;
      const session = sessionOf(file);
      const ts = typeof e.timestamp === "string" ? e.timestamp : "";
      const type = typeof e.type === "string" ? e.type : "";
      const blocks = blocksOf(e);
      const key = type === "assistant" ? null : dedupeKey(ts, blocks);
      if (key !== null) {
        if (seen.has(key)) return;
        own.push(key);
      }
      lines.push({ file, session, n: i + 1, ref: `transcript:${session}#${i + 1}`, ts, type, branch: typeof e.gitBranch === "string" ? e.gitBranch : "", blocks, tools: toolNames(contentOf(e)) });
    });
    for (const k of own) seen.add(k);
  }
  return { lines, files: recent.length, skipped: all.length - recent.length };
}

export function parseSince(v: string | undefined, now: Date): Date {
  const m = /^(\d{1,3})d$/.exec(v ?? "7d");
  if (m === null || Number(m[1]) < 1) throw new SindriError("SND-CLI-002", "--since must look like 7d (a whole number of days)");
  return new Date(now.getTime() - Number(m[1]) * 86_400_000);
}

// sources.transcripts.dir, with ~ expanded. Evolve reads it whether or not sources.transcripts.enabled
// is on: that flag governs scoping, and evolve only ever reads this repo's sessions.
export const transcriptsDir = (ctx: EvolveCtx): string => ctx.loaded.profile.sources.transcripts.dir.replace(/^~(?=\/|$)/, ctx.deps.home);

// A short, scrubbed, single-line excerpt for a transcript:<session>#<line> ref (what `show` prints).
export function excerptFor(dir: string, ref: string, max = 300): string | null {
  const m = /^transcript:([A-Za-z0-9]{1,8})#(\d{1,7})$/.exec(ref);
  if (m === null || !fs.existsSync(dir)) return null;
  const files: string[] = [];
  walk(dir, files);
  for (const f of files.sort().filter((x) => sessionOf(x) === m[1])) {
    const e = parseEntry(fs.readFileSync(f, "utf8").split("\n")[Number(m[2]) - 1]);
    if (e === null) continue;
    const text = blocksOf(e).map((b) => b.text).join(" ").replace(/[\u0000-\u001f\u007f]+/g, " ").trim();
    if (text !== "") return scrubber.scrub(text).text.slice(0, max);
  }
  return null;
}
