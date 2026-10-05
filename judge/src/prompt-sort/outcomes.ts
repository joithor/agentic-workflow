// judge/src/prompt-sort/outcomes.ts
// Outcome labels for sorter axes, from what happened next in the session
// transcript (Joi hand-labels nothing). Independent of the sorter's own output.
// Uses Plan A's realPrompts/classifyReply/findTranscript (judge/src/outcomes.ts).
import fs from "node:fs";

import { recordLabel, type Db } from "../db.js";
import { classifyReply, findTranscript, realPrompts, type RealPrompt } from "../outcomes.js";
import { SORT_QUESTION_PREFIX, type AxisName } from "./axes.js";

export const LARGE_FILES = 8;
export const LARGE_EDITS = 25;
const NEW_TASK_MIN_WORDS = 10;
const SIX_HOURS_MS = 6 * 60 * 60 * 1000;
const EDIT_TOOLS = new Set(["Edit", "Write", "MultiEdit", "NotebookEdit"]);
const BUG_SKILLS = new Set(["bugfixorchestrator", "rootcause", "bughunt", "bugreport"]);
const UI_SKILLS = new Set(["ui-evidence", "verify-web", "verify-ios", "verify-app", "design-implement", "design-implement-web", "design-implement-ios"]);
const UI_FILE = /\.(?:tsx|jsx|css|scss)$|\/(?:components|views|pages|screens)\/|(?:View|Screen|Sheet|Cell)\w*\.swift$/i;
const CLARIFY = /\b(?:i meant|i mean|not that|instead|what i wanted|to clarify|i said|that'?s not)\b/i;
const COMMAND_NAME = /<command-name>\/?([^<\s]+)<\/command-name>/;

export interface WindowFacts {
  skills: string[]; editedFiles: string[]; editCalls: number; assistantTurns: number; nextPrompts: RealPrompt[]; closed: boolean;
}

function userText(content: unknown): string {
  if (typeof content === "string") return content;
  if (!Array.isArray(content)) return "";
  return content.filter((b) => (b as { type?: unknown }).type === "text").map((b) => String((b as { text?: unknown }).text ?? "")).join("\n");
}

export function windowFacts(jsonl: string, decidedAt: string): WindowFacts {
  const t0 = Date.parse(decidedAt);
  const limit = t0 + SIX_HOURS_MS;
  const after = realPrompts(jsonl).filter((p) => Date.parse(p.ts) > t0 && Date.parse(p.ts) <= limit);
  const topicChange = after.find((p) => classifyReply(p.text) === "other" && p.text.trim().split(/\s+/).length >= NEW_TASK_MIN_WORDS);
  const end = topicChange === undefined ? limit : Date.parse(topicChange.ts);
  const skills: string[] = [];
  const files = new Set<string>();
  let editCalls = 0;
  let assistantTurns = 0;
  for (const line of jsonl.split("\n")) {
    if (line.trim() === "") continue;
    let row: { type?: unknown; timestamp?: unknown; message?: { content?: unknown } };
    try {
      row = JSON.parse(line) as typeof row;
    } catch {
      continue;
    }
    const ts = typeof row.timestamp === "string" ? Date.parse(row.timestamp) : Number.NaN;
    if (!(ts > t0 && ts < end)) continue;
    if (row.type === "assistant") {
      assistantTurns++;
      const blocks = Array.isArray(row.message?.content) ? (row.message?.content as Array<{ type?: unknown; name?: unknown; input?: Record<string, unknown> }>) : [];
      for (const b of blocks) {
        if (b.type !== "tool_use" || typeof b.name !== "string") continue;
        if (EDIT_TOOLS.has(b.name)) {
          editCalls++;
          const file = b.input?.file_path ?? b.input?.notebook_path;
          if (typeof file === "string") files.add(file);
        } else if (b.name === "Skill" && typeof b.input?.skill === "string") {
          skills.push(b.input.skill.toLowerCase());
        }
      }
    } else if (row.type === "user") {
      const m = COMMAND_NAME.exec(userText(row.message?.content));
      if (m?.[1] !== undefined) skills.push(m[1].toLowerCase());
    }
  }
  // C11: prompts at or after the topic change belong to another task and must
  // not label this one (the changing prompt itself included).
  const inWindow = after.filter((p) => Date.parse(p.ts) < end);
  return { skills, editedFiles: [...files], editCalls, assistantTurns, nextPrompts: inWindow.slice(0, 2), closed: topicChange !== undefined };
}

export function sortOutcomeLabel(axis: AxisName, f: WindowFacts): string | null {
  switch (axis) {
    case "is_task":
      return f.editCalls > 0 ? "yes" : f.assistantTurns > 0 ? "no" : null;
    case "is_bug_report":
      return f.skills.some((s) => BUG_SKILLS.has(s)) ? "yes" : null; // recall-only
    case "touches_ui":
      if (f.skills.some((s) => UI_SKILLS.has(s)) || f.editedFiles.some((p) => UI_FILE.test(p))) return "yes";
      return f.editCalls > 0 ? "no" : null;
    case "complexity":
      if (f.editedFiles.length >= LARGE_FILES || f.editCalls >= LARGE_EDITS) return "large";
      return f.assistantTurns > 0 ? "not-large" : null;
    case "ambiguity":
      if (f.nextPrompts.length === 0) return null;
      return f.nextPrompts.some((p) => ["interrupt", "correction"].includes(classifyReply(p.text)) || CLARIFY.test(p.text)) ? "unclear" : "clear";
    default:
      return null;
  }
}

const NEGATIVE = new Set(["no", "not-large", "clear"]);

export function runSortOutcomeLabels(
  db: Db, opts: { projectsDir: string; now: () => Date; readFile?: (p: string) => string },
): { labeled: number; noSignal: number; pending: number } {
  const read = opts.readFile ?? ((p: string) => fs.readFileSync(p, "utf8"));
  // Derived-key join (C12): the item source is exactly decision:<id>:<axis>.
  const rows = db
    .prepare(
      `SELECT e.id AS item_id, e.question AS question, d.ts AS ts, x.session_id AS session_id
       FROM eval_items e
       JOIN decisions d ON e.source = 'decision:' || d.id || ':' || substr(e.question, ${SORT_QUESTION_PREFIX.length + 1})
       JOIN decision_details x ON x.id = d.id
       LEFT JOIN labels l ON l.item_id = e.id AND l.source = 'outcome'
       WHERE e.question LIKE 'prompt-sort:%' AND d.question = 'prompt-sort' AND l.item_id IS NULL`,
    )
    .all() as Array<{ item_id: string; question: string; ts: string; session_id: string | null }>;
  const cache = new Map<string, WindowFacts | null>();
  let labeled = 0;
  let noSignal = 0;
  let pending = 0;
  for (const r of rows) {
    const cacheKey = `${r.session_id ?? ""}\u0000${r.ts}`;
    let f = cache.get(cacheKey);
    if (f === undefined) {
      const file = r.session_id === null ? undefined : findTranscript(opts.projectsDir, r.session_id);
      f = file === undefined ? null : windowFacts(read(file), r.ts);
      cache.set(cacheKey, f);
    }
    if (f === null) {
      noSignal++;
      continue;
    }
    const label = sortOutcomeLabel(r.question.slice(SORT_QUESTION_PREFIX.length) as AxisName, f);
    if (label === null) {
      noSignal++;
      continue;
    }
    // Negative labels are only final once the window closed or 6h passed (RF-5).
    const aged = opts.now().getTime() - Date.parse(r.ts) >= SIX_HOURS_MS;
    if (NEGATIVE.has(label) && !f.closed && !aged) {
      pending++;
      continue;
    }
    recordLabel(db, r.item_id, label, opts.now().toISOString(), "outcome");
    labeled++;
  }
  return { labeled, noSignal, pending };
}
