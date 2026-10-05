// judge/src/prompt-sort/heuristics.ts
// Keyword fallback for every sorter axis: a TypeScript port of the useful parts
// of Navigator's hooks/nav_hook_lib/scoring.py (task shape, ambiguity credits,
// additive complexity, loop triggers) plus keyword detectors for the three
// scaffold-driving axes. Deterministic: same prompt, same answer. This is the
// baseline the judge blend has to beat (Task 9), so it is deliberately plain.
import type { Ambiguity, Complexity, SortValues } from "./axes.js";

const QUESTION_STARTERS = new Set([
  "what", "why", "how", "when", "where", "who", "which", "can", "could", "should",
  "is", "are", "do", "does", "did", "will", "would",
]);
const CONFIRMATIONS = [
  "yes", "ok", "okay", "sure", "sounds good", "go ahead", "proceed", "continue", "looks good", "lgtm",
  "do it", "correct", "confirmed", "approved", "agreed", "no", "nope", "cancel", "stop",
];
const CONFIRMATION_MAX_WORDS = 6;
const TASK_VERBS = [
  "add", "create", "build", "implement", "refactor", "fix", "update", "change", "improve", "enhance",
  "redesign", "migrate", "integrate", "optimize", "rewrite", "replace", "remove", "delete", "write",
  "design", "develop", "extend", "modify", "generate", "configure", "set up", "clean up",
];
const VAGUE_SCOPE = [
  "the app", "the system", "the api", "the codebase", "the platform", "the backend", "the frontend", "the ui",
  "the project", "the entire", "everything", "all of it", "the whole thing",
  "endpoints", "components", "tests", "files", "bugs", "issues", "features",
];
const LIMITERS = ["only", "just", "specifically", "excluding", "except", "up to", "no more than", "limit to", "solely"];
const ACCEPTANCE = [
  "acceptance criteria", "when done", "success looks like", "verify with", "verify that", "done when", "definition of done",
];
// Navigator also lists "with"; it matches nearly every sentence, so it is left out.
const APPROACH = ["using", "via", "through", "by using", "based on"];
const LOOP_TRIGGERS = [
  "run until done", "do all", "do it all", "keep going", "iterate until", "finish this", "complete everything",
  "don't stop", "dont stop", "until complete", "until finished", "until done", "loop mode", "autonomous mode",
];
const HIGH = ["refactor", "implement", "add feature", "new feature", "architecture", "redesign", "migrate", "overhaul"];
const MEDIUM = ["fix all", "update all", "change all", "modify", "enhance", "improve", "extend", "integrate"];
const LOW = ["add", "create", "update", "fix", "change", "remove", "delete"];
const MULTI_FILE = [
  "multiple files", "several files", "across", "all files", "everywhere", "throughout", "project-wide", "codebase",
];

const PATH_RE = /[\w.-]+\/[\w./-]+/;
const FILE_RE = /\b[\w-]+\.(?:py|ts|tsx|js|jsx|md|json|yaml|yml|go|rb|java|rs|css|html|swift)\b/i;
const NUMBER_RE = /\b\d+(?:\.\d+)?\b/;
const BACKTICK_RE = /`[^`]+`/;
const VERIFY_RE = /\b(tests?|typecheck|passes|passing|verify|verified|lint|screenshot|acceptance|expected)\b/;
const BUG_RE =
  /\b(bugs?|broken|crash(?:es|ed|ing)?|regression|exception|stack trace|traceback|fails|failing|failed|doesn'?t work|not working|isn'?t working|throws|wrong|incorrect)\b|\berror\b/;
const UI_RE =
  /\b(ui|ux|screen|page|button|modal|dialog|layout|css|style|styling|styles|components?|swiftui|tab|dropdown|form|responsive|dark mode|spacing|icon|navbar|sidebar|tooltip|toast|animation)\b/;
const UI_FILE_RE = /\.(?:tsx|jsx|css|scss|swift)\b/i;
const RESEARCH_RE = /\b(investigate|research|find out|look into|figure out|explore|compare|root cause|why (?:does|is|are|do))\b/;

const BASE_SCORE = 0.5;
const VAGUE_BONUS = 0.2;
const CREDIT_FILE = 0.4;
const CREDIT_NUMBER = 0.2;
const CREDIT_LIMITER = 0.2;
const CREDIT_ACCEPTANCE = 0.3;

const phraseCache = new Map<string, RegExp>();
function phraseRe(phrase: string): RegExp {
  let re = phraseCache.get(phrase);
  if (re === undefined) {
    re = new RegExp(`\\b${phrase.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/ /g, "\\s+")}\\b`);
    phraseCache.set(phrase, re);
  }
  return re;
}
const hasPhrase = (text: string, phrase: string): boolean => phraseRe(phrase).test(text);
const firstMatch = (text: string, phrases: readonly string[]): string => phrases.find((p) => hasPhrase(text, p)) ?? "";
const wordsOf = (text: string): string[] => text.match(/[a-z']+/g) ?? [];

function isQuestion(text: string): boolean {
  if (text.trimEnd().endsWith("?")) return true;
  return QUESTION_STARTERS.has(wordsOf(text)[0] ?? "");
}

export function isConfirmation(text: string): boolean {
  const words = wordsOf(text.toLowerCase());
  if (words.length === 0 || words.length > CONFIRMATION_MAX_WORDS) return false;
  const normalized = words.join(" ");
  return CONFIRMATIONS.some((c) => {
    const bare = wordsOf(c).join(" ");
    return normalized === bare || normalized.startsWith(`${bare} `);
  });
}

function isTaskShaped(text: string): boolean {
  if (text === "" || isQuestion(text) || isConfirmation(text)) return false;
  return firstMatch(text, TASK_VERBS) !== "";
}

function complexityLevel(text: string, task: boolean): Complexity {
  if (!task) return "trivial";
  let score = 0;
  for (const p of HIGH) if (hasPhrase(text, p)) score += 0.3;
  for (const p of MEDIUM) if (hasPhrase(text, p)) score += 0.2;
  for (const p of LOW) if (hasPhrase(text, p)) score += 0.1;
  if (MULTI_FILE.some((p) => hasPhrase(text, p))) score += 0.2;
  const rounded = Math.round(Math.min(score, 1) * 100) / 100;
  if (rounded < 0.2) return "trivial";
  if (rounded < 0.5) return "small";
  if (rounded < 0.8) return "substantial";
  return "large";
}

function ambiguityLevel(raw: string, text: string, task: boolean): Ambiguity {
  if (!task) return "clear";
  let score = BASE_SCORE;
  if (firstMatch(text, VAGUE_SCOPE) !== "") score += VAGUE_BONUS;
  if (PATH_RE.test(raw) || FILE_RE.test(raw)) score -= CREDIT_FILE;
  if (NUMBER_RE.test(text)) score -= CREDIT_NUMBER;
  if (firstMatch(text, LIMITERS) !== "") score -= CREDIT_LIMITER;
  if (firstMatch(text, ACCEPTANCE) !== "") score -= CREDIT_ACCEPTANCE;
  const rounded = Math.round(Math.max(0, Math.min(1, score)) * 100) / 100;
  if (rounded >= 0.5) return "vague";
  if (rounded >= 0.2) return "partly";
  return "clear";
}

export function heuristicSort(prompt: string): SortValues {
  const text = prompt.toLowerCase().trim();
  const task = isTaskShaped(text);
  return {
    is_task: task,
    wants_loop: LOOP_TRIGGERS.some((t) => hasPhrase(text, t)),
    scope_defined: PATH_RE.test(prompt) || FILE_RE.test(prompt) || BACKTICK_RE.test(prompt),
    limits_defined: firstMatch(text, LIMITERS) !== "" || firstMatch(text, ACCEPTANCE) !== "",
    approach_defined: firstMatch(text, APPROACH) !== "",
    verification_defined: firstMatch(text, ACCEPTANCE) !== "" || VERIFY_RE.test(text),
    is_bug_report: BUG_RE.test(text),
    touches_ui: UI_RE.test(text) || UI_FILE_RE.test(prompt),
    needs_research: RESEARCH_RE.test(text),
    complexity: complexityLevel(text, task),
    ambiguity: ambiguityLevel(prompt, text, task),
  };
}
