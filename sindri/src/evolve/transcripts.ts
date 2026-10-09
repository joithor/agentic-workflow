import fs from "node:fs";
import path from "node:path";

import { SindriError } from "../errors.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";
import type { EvolveCtx } from "./ctx.js";

const scrubber = makeScrubber();

export interface Block {
  text: string;
  toolResult: boolean;
  isError?: boolean; // tool_result blocks only: the harness sets is_error on a hook's block
}

export interface SessionLine {
  file: string;
  session: string;
  n: number;
  ref: string;
  ts: string;
  type: string;
  meta: boolean; // the entry's isMeta flag: the harness marks the feedback it injects itself
  branch: string;
  blocks: Block[];
  tools: string[]; // names of the entry's tool_use blocks (an assistant line that edited a file has "Edit", "Write", ...)
}

interface RawEntry {
  type?: unknown;
  timestamp?: unknown;
  cwd?: unknown;
  isMeta?: unknown;
  gitBranch?: unknown;
  content?: unknown;
  message?: { content?: unknown };
}

export function textBlocks(content: unknown): Block[] {
  if (typeof content === "string") return [{ text: content, toolResult: false }];
  if (!Array.isArray(content)) return [];
  return content.flatMap((b: unknown): Block[] => {
    if (typeof b !== "object" || b === null) return [];
    const blk = b as { type?: unknown; text?: unknown; content?: unknown; is_error?: unknown };
    if (blk.type === "text" && typeof blk.text === "string") return [{ text: blk.text, toolResult: false }];
    if (blk.type === "tool_result") return [{ text: textBlocks(blk.content).map((x) => x.text).join("\n"), toolResult: true, isError: blk.is_error === true }];
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

const alnum = (s: string): string => s.replace(/[^A-Za-z0-9]/g, "");

// transcript:<first 8 characters of the session file name>#<line>: no project directory names. A
// subagent file (<session>/subagents/agent-<id>.jsonl) is <session prefix>.<agent id>, so two agents
// of one session (or of two sessions with the same prefix) never share a ref.
export function sessionOf(file: string): string {
  const base = path.basename(file, ".jsonl");
  const dir = path.dirname(file);
  if (path.basename(dir) !== "subagents") return alnum(base).slice(0, 8);
  return `${alnum(path.basename(path.dirname(dir))).slice(0, 8)}.${alnum(base.replace(/^agent-/, "")).slice(0, 40)}`;
}

// Both sides are resolved, so `/x/repo/../other` is not under `/x/repo` and a trailing slash on
// the profile path changes nothing. A relative or empty cwd is never under the repo.
function underRepo(cwd: string, repo: string): boolean {
  if (!path.isAbsolute(cwd)) return false;
  const [c, r] = [path.resolve(cwd), path.resolve(repo)];
  return c === r || c.startsWith(r.endsWith("/") ? r : `${r}/`);
}

function parseEntry(raw: string): RawEntry | null {
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

const tsOf = (e: RawEntry): string => (typeof e.timestamp === "string" ? e.timestamp : "");

// Only sessions of this repo (spec amendment 6): a line counts while the latest cwd seen in its
// file is the repo or under it. Files not modified since `since` aren't read. A non-assistant line
// already seen (same timestamp and text) in an earlier file of this scan is a copy and is skipped.
// "Earlier" is by each file's first timestamp, then its last (a resumed session starts with copies
// of the original's lines but runs longer), then name: file names are random, so they don't say
// which file is the original.
export function readRepoSessions(dir: string, repo: string, since: Date, maxFiles = 500): { lines: SessionLine[]; files: number; skipped: number } {
  if (!fs.existsSync(dir)) return { lines: [], files: 0, skipped: 0 };
  const all: string[] = [];
  walk(dir, all);
  const recent = all.map((f) => ({ f, m: fs.statSync(f).mtimeMs })).filter((x) => x.m >= since.getTime()).sort((a, b) => b.m - a.m).slice(0, maxFiles).map((x) => x.f);
  const parsed = recent.map((file) => {
    const entries = fs.readFileSync(file, "utf8").split("\n").map((raw, i) => ({ n: i + 1, e: parseEntry(raw) }));
    let first = Infinity;
    let last = -Infinity;
    for (const x of entries) {
      const t = x.e === null ? NaN : Date.parse(tsOf(x.e));
      if (Number.isNaN(t)) continue;
      first = Math.min(first, t);
      last = Math.max(last, t);
    }
    return { file, entries, first, last: last === -Infinity ? Infinity : last };
  }).sort((a, b) => a.first - b.first || a.last - b.last || (a.file < b.file ? -1 : 1));
  const lines: SessionLine[] = [];
  const seen = new Set<string>(); // keys of lines in earlier files
  for (const { file, entries } of parsed) {
    let cwd = "";
    const own: string[] = []; // added to `seen` only after the file, so a file never dedupes against itself
    const session = sessionOf(file);
    for (const { n, e } of entries) {
      if (e === null) continue;
      if (typeof e.cwd === "string") cwd = e.cwd;
      if (!underRepo(cwd, repo)) continue;
      const ts = tsOf(e);
      const type = typeof e.type === "string" ? e.type : "";
      const blocks = blocksOf(e);
      const key = type === "assistant" ? null : dedupeKey(ts, blocks);
      if (key !== null) {
        if (seen.has(key)) continue;
        own.push(key);
      }
      lines.push({ file, session, n, ref: `transcript:${session}#${n}`, ts, type, meta: e.isMeta === true, branch: typeof e.gitBranch === "string" ? e.gitBranch : "", blocks, tools: toolNames(contentOf(e)) });
    }
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
// Refs resolve only through this repo's sessions (Review Focus 5), so another repo's line is never
// printed even when its session prefix matches.
export function excerptFor(dir: string, repo: string, ref: string, max = 300, scrub: Scrubber = scrubber): string | null {
  const line = readRepoSessions(dir, repo, new Date(0), Infinity).lines.find((l) => l.ref === ref);
  if (line === undefined) return null;
  const text = line.blocks.map((b) => b.text).join(" ").replace(/[\u0000-\u001f\u007f]+/g, " ").trim();
  return text === "" ? null : scrub.scrub(text).text.slice(0, max);
}
