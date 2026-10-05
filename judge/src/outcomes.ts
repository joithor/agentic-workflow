// Outcome labels: what Joi did next tells us whether a decision was right,
// with no labeling work from Joi. Only the action taken is used (its
// consequences are what we observe); never the confidence or provider.
import fs from "node:fs";
import path from "node:path";

import { recordLabel, type Db } from "./db.js";

export interface RealPrompt { ts: string; text: string }

const SIX_HOURS_MS = 6 * 60 * 60 * 1000;
const INTERRUPT = "[Request interrupted by user";
const CORRECTION = /^\s*(no\b|nope|wait\b|stop\b|don'?t\b|actually\b|that'?s (wrong|not)|not what|undo|revert)/i;
const COMPACTION_SUMMARY = "This session is being continued from a previous conversation";
const BARE_CONTINUE = /^\s*(continue|keep going|go on|go ahead|proceed|yes|yep|y|ok|okay|do it|carry on)[\s.!]*$/i;

export function promptText(content: unknown): string | null {
  if (typeof content === "string") return content;
  if (!Array.isArray(content)) return null;
  if (content.some((b) => (b as { type?: unknown }).type === "tool_result")) return null;
  const texts = content.filter((b) => (b as { type?: unknown }).type === "text").map((b) => String((b as { text?: unknown }).text ?? ""));
  return texts.length === 0 ? null : texts.join("\n");
}

// Machine text is not a real prompt: system reminders, command wrappers and
// the summary Claude Code injects after a compaction.
export function isRealPromptText(text: string): boolean {
  const t = text.trimStart();
  return !t.startsWith("<") && !t.startsWith(COMPACTION_SUMMARY);
}

export function realPrompts(jsonl: string): RealPrompt[] {
  const out: RealPrompt[] = [];
  for (const line of jsonl.split("\n")) {
    if (line.trim() === "") continue;
    let row: { type?: unknown; timestamp?: unknown; message?: { content?: unknown } };
    try {
      row = JSON.parse(line) as typeof row;
    } catch {
      continue;
    }
    if (row.type !== "user" || typeof row.timestamp !== "string") continue;
    const text = promptText(row.message?.content);
    if (text === null || !isRealPromptText(text)) continue;
    out.push({ ts: row.timestamp, text });
  }
  return out;
}

export function classifyReply(text: string): "interrupt" | "correction" | "bare-continue" | "other" {
  if (text.includes(INTERRUPT)) return "interrupt";
  if (CORRECTION.test(text)) return "correction";
  if (BARE_CONTINUE.test(text)) return "bare-continue";
  return "other";
}

export function askCheckOutcome(action: "continue" | "ask", decidedAt: string, prompts: readonly RealPrompt[]): "continue" | "ask" | null {
  const t0 = Date.parse(decidedAt);
  const next = prompts.find((p) => Date.parse(p.ts) > t0);
  if (next === undefined || Date.parse(next.ts) - t0 > SIX_HOURS_MS) return null;
  const kind = classifyReply(next.text);
  if (action === "continue") return kind === "interrupt" || kind === "correction" ? "ask" : "continue";
  return kind === "bare-continue" ? "continue" : "ask";
}

// Fails open: a missing or unreadable projects dir means no transcript.
export function findTranscript(projectsDir: string, sessionId: string): string | undefined {
  let dirs: string[];
  try {
    dirs = fs.readdirSync(projectsDir);
  } catch {
    return undefined;
  }
  for (const dir of dirs) {
    const candidate = path.join(projectsDir, dir, `${sessionId}.jsonl`);
    if (fs.existsSync(candidate)) return candidate;
  }
  return undefined;
}

export function runOutcomeLabels(
  db: Db,
  opts: { projectsDir: string; now: () => Date; readFile?: (p: string) => string },
): { labeled: number; noSignal: number } {
  const read = opts.readFile ?? ((p: string) => fs.readFileSync(p, "utf8"));
  const rows = db.prepare(
    `SELECT e.id AS item_id, d.ts, d.decision, x.session_id FROM eval_items e
     JOIN decisions d ON e.source = 'decision:' || d.id
     JOIN decision_details x ON x.id = d.id
     LEFT JOIN labels l ON l.item_id = e.id AND l.source = 'outcome'
     WHERE e.question = 'ask-check' AND l.item_id IS NULL`,
  ).all() as Array<{ item_id: string; ts: string; decision: "continue" | "ask"; session_id: string | null }>;
  let labeled = 0;
  let noSignal = 0;
  for (const r of rows) {
    let label: "continue" | "ask" | null = null;
    try {
      const file = r.session_id === null ? undefined : findTranscript(opts.projectsDir, r.session_id);
      if (file !== undefined) label = askCheckOutcome(r.decision, r.ts, realPrompts(read(file)));
    } catch {
      label = null;
    }
    if (label === null) {
      noSignal++;
      continue;
    }
    recordLabel(db, r.item_id, label, opts.now().toISOString(), "outcome");
    labeled++;
  }
  return { labeled, noSignal };
}
