// Per-turn eval items rebuilt from a transcript's Edit/Write/MultiEdit calls
// (Plan B Task 4). Outcome labels come from what happened next in the same
// transcript (line survival, then the next real prompt): no human labeling,
// and nothing here reads or writes `decisions`.
import crypto from "node:crypto";
import fs from "node:fs";

import { getBriefByToolUseId, recordLabel, upsertEvalItem, type Db } from "./db.js";
import { classifyReply, isRealPromptText, promptText } from "./outcomes.js";
import { INPUT_CAP, capJsonValue, redactDeep } from "./redact.js";
import type { TurnProgressInput } from "./questions/turn-progress.js";

export interface Turn {
  sessionId: string;
  index: number;
  prompt: string;
  edits: Array<{ file: string; before: string; after: string }>;
}

const SESSION_CLOSED_MS = 6 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;

type Edit = Turn["edits"][number];
type Row = { type?: unknown; sessionId?: unknown; timestamp?: unknown; message?: { content?: unknown } };

const str = (v: unknown): string => (typeof v === "string" ? v : "");

function editsOf(block: { name?: unknown; input?: unknown }): Edit[] {
  const input = (block.input ?? {}) as Record<string, unknown>;
  const file = str(input.file_path);
  if (block.name === "Edit") return [{ file, before: str(input.old_string), after: str(input.new_string) }];
  if (block.name === "Write") return [{ file, before: "", after: str(input.content) }];
  if (block.name === "MultiEdit" && Array.isArray(input.edits)) {
    return input.edits.map((e: Record<string, unknown>) => ({ file, before: str(e.old_string), after: str(e.new_string) }));
  }
  return [];
}

function parseSessions(jsonl: string): { turns: Turn[]; ends: Map<string, number> } {
  const turns: Turn[] = [];
  const ends = new Map<string, number>();
  const current = new Map<string, Turn>();
  const counts = new Map<string, number>();
  for (const line of jsonl.split("\n")) {
    if (line.trim() === "") continue;
    let row: Row;
    try {
      row = JSON.parse(line) as Row;
    } catch {
      continue;
    }
    const sessionId = str(row.sessionId) || "unknown";
    const ts = Date.parse(str(row.timestamp));
    if (!Number.isNaN(ts)) ends.set(sessionId, Math.max(ends.get(sessionId) ?? 0, ts));
    if (row.type === "user") {
      const text = promptText(row.message?.content);
      if (text === null || !isRealPromptText(text)) continue;
      const index = counts.get(sessionId) ?? 0;
      counts.set(sessionId, index + 1);
      const turn: Turn = { sessionId, index, prompt: text, edits: [] };
      current.set(sessionId, turn);
      turns.push(turn);
    } else if (row.type === "assistant" && Array.isArray(row.message?.content)) {
      for (const block of row.message.content as Array<{ type?: unknown; name?: unknown; input?: unknown }>) {
        if (block.type === "tool_use") current.get(sessionId)?.edits.push(...editsOf(block));
      }
    }
  }
  return { turns, ends };
}

export function parseTurns(jsonl: string): Turn[] {
  return parseSessions(jsonl).turns;
}

export function turnDiff(edits: Turn["edits"]): string {
  const out: string[] = [];
  for (const e of edits) {
    out.push(`--- ${e.file}`);
    if (e.before !== "") out.push(...e.before.split("\n").map((l) => `- ${l}`));
    out.push(...e.after.split("\n").map((l) => `+ ${l}`));
  }
  return out.join("\n");
}

// Blank and brace-only lines carry no signal and survive trivially.
const TRIVIAL_LINE = /^[\s{}()[\];,]*$/;

export function survival(turns: readonly Turn[], index: number): number | null {
  const added: Array<{ file: string; line: string; later: Edit[] }> = [];
  const own = turns[index]?.edits ?? [];
  own.forEach((e, i) => {
    const after = [...own.slice(i + 1), ...turns.slice(index + 1).flatMap((t) => t.edits)].filter((x) => x.file === e.file);
    for (const line of e.after.split("\n")) if (!TRIVIAL_LINE.test(line)) added.push({ file: e.file, line, later: after });
  });
  if (added.length === 0) return null;
  const dead = (line: string, edits: Edit[]): boolean =>
    edits.some((x) => (x.before === "" ? !x.after.split("\n").includes(line) : x.before.split("\n").includes(line) && !x.after.split("\n").includes(line)));
  return added.filter((a) => !dead(a.line, a.later)).length / added.length;
}

export function turnOutcome(turns: readonly Turn[], index: number, sessionClosed = false): "progressing" | "stalled" | null {
  const s = survival(turns, index);
  if (s === null) return null;
  const next = turns[index + 1];
  if (next === undefined && !sessionClosed) return null;
  const kind = next === undefined ? "other" : classifyReply(next.prompt);
  if (s < 0.3 || kind === "interrupt" || kind === "correction") return "stalled";
  return s >= 0.7 ? "progressing" : null;
}

export function importTurns(db: Db, jsonl: string, now: () => Date): { imported: number; skipped: number } {
  const { turns, ends } = parseSessions(jsonl);
  const exists = db.prepare("SELECT 1 FROM eval_items WHERE question = 'turn-progress' AND source = ?");
  let imported = 0;
  let skipped = 0;
  for (const turn of turns) {
    const source = `transcript:${turn.sessionId}:${turn.index}`;
    if (turn.edits.length === 0 || exists.get(source) !== undefined) {
      skipped++;
      continue;
    }
    const session = turns.filter((t) => t.sessionId === turn.sessionId);
    const brief = getBriefByToolUseId(db, `task:${turn.sessionId}`);
    const earlier = new Set(session.slice(0, turn.index).flatMap((t) => t.edits.map((e) => e.file)));
    const input: TurnProgressInput = {
      problem: brief === undefined ? turn.prompt : `Goal: ${brief.goal}\n\n${turn.prompt}`,
      acceptanceCriteria: brief?.acceptanceCriteria ?? "",
      turnDiff: turnDiff(turn.edits),
      priorDiffStat: `${earlier.size} files edited earlier in session`,
      signals: "",
    };
    const id = crypto.randomUUID();
    const at = now();
    upsertEvalItem(db, { id, question: "turn-progress", input_json: capJsonValue(redactDeep(input), INPUT_CAP), source, model_decision: null, created_at: at.toISOString() });
    imported++;
    const end = ends.get(turn.sessionId);
    const closed = end !== undefined && at.getTime() - end >= SESSION_CLOSED_MS;
    const outcome = turnOutcome(session, turn.index, closed);
    if (outcome !== null) recordLabel(db, id, outcome, at.toISOString(), "outcome");
  }
  return { imported, skipped };
}

// `judge eval import-turns <transcript.jsonl>... [--since 14d]`; --since keeps
// files modified within that many days.
export function runImportTurns(db: Db, args: readonly string[], now: () => Date): { exitCode: number; stdout: string; stderr?: string } {
  const files: string[] = [];
  let sinceMs: number | undefined;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--since") {
      const m = /^(\d+)d$/.exec(args[++i] ?? "");
      if (m === null) return { exitCode: 1, stdout: "", stderr: "--since must look like 14d" };
      sinceMs = Number(m[1]) * DAY_MS;
    } else files.push(args[i]);
  }
  if (files.length === 0) return { exitCode: 1, stdout: "", stderr: "usage: judge eval import-turns <transcript.jsonl>... [--since 14d]" };
  const total = { imported: 0, skipped: 0 };
  for (const file of files) {
    try {
      if (sinceMs !== undefined && now().getTime() - fs.statSync(file).mtimeMs > sinceMs) continue;
      const r = importTurns(db, fs.readFileSync(file, "utf8"), now);
      total.imported += r.imported;
      total.skipped += r.skipped;
    } catch (e) {
      return { exitCode: 1, stdout: "", stderr: `cannot read ${file}: ${(e as Error).message}` };
    }
  }
  return { exitCode: 0, stdout: JSON.stringify(total) };
}
