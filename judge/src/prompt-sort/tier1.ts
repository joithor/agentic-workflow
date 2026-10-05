// judge/src/prompt-sort/tier1.ts
// Zero-call answers: prompts the sorter must not spend a Jev request on, and
// must never scaffold (RF-3). The machine prefixes mirror turn-origin.sh and
// scorer/src/transcript/classify.ts (judge does not import scorer).
import { isConfirmation } from "./heuristics.js";

export type SkipReason = "empty" | "machine" | "slash-command" | "confirmation" | "too-short";

const MACHINE_PREFIXES = [
  "<", "[Request interrupted by user", "Another Claude session sent a message", "Base directory for this skill",
  "Caveat:", "This session is being continued",
];
// "/bugFixOrchestrator FRN-1", "/clear": a slash word followed by space or end.
// A pasted absolute path ("/Users/joi/x") has "/" after the first segment and does not match.
const SLASH_COMMAND = /^\/[A-Za-z][\w:-]*(\s|$)/;
const MIN_CHARS = 4;

export function tier1Skip(prompt: string): SkipReason | null {
  const text = prompt.trim();
  if (text === "") return "empty";
  if (MACHINE_PREFIXES.some((p) => text.startsWith(p))) return "machine";
  if (SLASH_COMMAND.test(text)) return "slash-command";
  if (text.length < MIN_CHARS) return "too-short";
  if (isConfirmation(text)) return "confirmation";
  return null;
}
