import fs from "node:fs";
import readline from "node:readline";

import { classifyUserText } from "../transcript/classify.js";

export interface HumanTurn {
  project: string;
  session: string;
  ts: string;
  index: number;
  kind: "turn" | "command" | "interrupt";
  text: string;
  skills: string[];
  guardFiredBefore: boolean;
  compactedBefore: boolean;
  editsBefore: boolean;
  contextTokens: number;
  prevAssistantTail: string;
}

const GUARD_MARK = "token guard)";
const COMPACTED = "This session is being continued";
const CMD = /<command-name>\/?([^<]+)<\/command-name>/;
const CMD_ARGS = /<command-args>([\s\S]*?)<\/command-args>/;
const MAX_TEXT = 4000;
const TAIL = 700;
const EDIT_TOOLS: ReadonlySet<string> = new Set(["Edit", "Write", "MultiEdit", "NotebookEdit"]);

type Block = { type?: unknown; text?: unknown; name?: unknown; input?: unknown };

function textOf(content: unknown): { text: string; toolResult: boolean } {
  if (typeof content === "string") return { text: content, toolResult: false };
  if (!Array.isArray(content)) return { text: "", toolResult: false };
  const parts: string[] = [];
  let toolResult = false;
  for (const b of content as Block[]) {
    if (b?.type === "text" && typeof b.text === "string") parts.push(b.text);
    else if (b?.type === "tool_result") toolResult = true;
  }
  return { text: parts.join("\n"), toolResult };
}

function skillsUsed(content: unknown): string[] {
  if (!Array.isArray(content)) return [];
  const out: string[] = [];
  for (const b of content as Block[]) {
    if (b?.type === "tool_use" && b.name === "Skill" && typeof b.input === "object" && b.input !== null) {
      const skill = (b.input as { skill?: unknown }).skill;
      if (typeof skill === "string") out.push(skill);
    }
  }
  return out;
}

function editToolUsed(content: unknown): boolean {
  if (!Array.isArray(content)) return false;
  return (content as Block[]).some((b) => b?.type === "tool_use" && typeof b.name === "string" && EDIT_TOOLS.has(b.name));
}

export async function* extractHumanTurns(file: { path: string; project: string; sessionId: string }): AsyncGenerator<HumanTurn> {
  const rl = readline.createInterface({ input: fs.createReadStream(file.path, { encoding: "utf8" }), crlfDelay: Infinity });
  const skills: string[] = [];
  let guard = false;
  let compacted = false;
  let edits = false;
  let tokens = 0;
  let prev = "";
  let index = 0;
  for await (const line of rl) {
    if (line.includes(GUARD_MARK)) guard = true;
    let rec: { type?: unknown; isSidechain?: unknown; isMeta?: unknown; timestamp?: unknown; message?: { content?: unknown; usage?: Record<string, unknown> } };
    try {
      const parsed: unknown = JSON.parse(line);
      if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) continue;
      rec = parsed as typeof rec;
    } catch {
      continue;
    }
    if (rec.isSidechain === true) continue;
    if (rec.type === "assistant") {
      const u = rec.message?.usage ?? {};
      const n = ["input_tokens", "cache_read_input_tokens", "cache_creation_input_tokens"].reduce((s, k) => s + (typeof u[k] === "number" ? (u[k] as number) : 0), 0);
      if (n > 0) tokens = n;
      const { text } = textOf(rec.message?.content);
      if (text.trim()) prev = text.trim();
      for (const s of skillsUsed(rec.message?.content)) if (!skills.includes(s)) skills.push(s);
      if (!edits && editToolUsed(rec.message?.content)) edits = true; // sidechains were skipped above
      continue;
    }
    if (rec.type !== "user" || rec.isMeta === true) continue;
    const { text, toolResult } = textOf(rec.message?.content);
    const s = text.trim();
    if (toolResult && !s) continue;
    if (s.startsWith(COMPACTED)) { compacted = true; continue; }
    let kind: HumanTurn["kind"] = "turn";
    let body = s;
    const m = CMD.exec(s);
    if (m) {
      const name = m[1].trim();
      if (!skills.includes(name)) skills.push(name);
      body = `/${name} ${(CMD_ARGS.exec(s)?.[1] ?? "").trim()}`.trim();
      kind = "command";
    } else {
      const c = classifyUserText(s);
      if (c.kind === "interrupt") kind = "interrupt";
      else if (c.kind !== "user") continue;
    }
    index += 1;
    yield {
      project: file.project, session: file.sessionId, ts: typeof rec.timestamp === "string" ? rec.timestamp : "",
      index, kind, text: body.slice(0, MAX_TEXT), skills: [...skills], guardFiredBefore: guard,
      compactedBefore: compacted, editsBefore: edits, contextTokens: tokens, prevAssistantTail: prev.slice(-TAIL),
    };
  }
}
