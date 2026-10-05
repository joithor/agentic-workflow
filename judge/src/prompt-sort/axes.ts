// judge/src/prompt-sort/axes.ts
// The 11 questions the prompt sorter asks about every real user prompt. Wording
// follows Navigator's hooks/nav_hook_lib/judge.py QUESTIONS, plus three
// scaffold-driving axes (is_bug_report, touches_ui, needs_research).
import type { JevQuestion } from "../providers/jev-api.js";

export const NOUL_AXES = [
  "is_task", "wants_loop", "scope_defined", "limits_defined", "approach_defined",
  "verification_defined", "is_bug_report", "touches_ui", "needs_research",
] as const;
export type NoulAxis = (typeof NOUL_AXES)[number];
export const SCORE_AXES = ["complexity", "ambiguity"] as const;
export type ScoreAxis = (typeof SCORE_AXES)[number];
export type AxisName = NoulAxis | ScoreAxis;
export const ALL_AXES: readonly AxisName[] = [...NOUL_AXES, ...SCORE_AXES];

export const COMPLEXITY_LEVELS = ["trivial", "small", "substantial", "large"] as const;
export const AMBIGUITY_LEVELS = ["clear", "partly", "vague"] as const;
export type Complexity = (typeof COMPLEXITY_LEVELS)[number];
export type Ambiguity = (typeof AMBIGUITY_LEVELS)[number];

// Ordered rubric levels, "name: description" (Jev reads the descriptions).
export const COMPLEXITY_CRITERIA: readonly string[] = [
  "trivial: a one-line answer or a single small edit",
  "small: one file, under an hour of work",
  "substantial: several files, a new feature, or a refactor of one module",
  "large: a cross-cutting refactor, a new subsystem, or multi-day work",
];
export const AMBIGUITY_CRITERIA: readonly string[] = [
  "clear: scope, target files or components, and done-criteria are explicit",
  "partly: the goal is clear but scope, limits, or done-criteria are missing",
  "vague: the goal itself is open to interpretation",
];
// Level weights for the 0..1 ambiguity value (Navigator eval 2026-09-19: "partly"
// must sit below the 0.5 line on its own; only real "vague" mass crosses it).
export const AMBIGUITY_WEIGHTS = [0, 0.35, 1] as const;

// Decisive band (Navigator, swept 2026-09-19): a noul counts only outside
// [low, high]; a score counts only at confidence >= minConfidence.
export const BAND = { low: 0.4, high: 0.6, minConfidence: 0.4 } as const;

export type SortValues = Record<NoulAxis, boolean> & { complexity: Complexity; ambiguity: Ambiguity };
export type AxisValue = boolean | Complexity | Ambiguity;

export const NOUL_INSTRUCTIONS: Record<NoulAxis, string> = {
  is_task:
    "The message asks the assistant to change something: write or edit code, files, configuration, or documents. Questions, chat, status reports, pasted logs, and replies confirming or answering the assistant do not count.",
  wants_loop:
    "The user explicitly asks for unattended, autonomous iteration until the work is finished (for example 'run until done', 'keep going until it passes', 'do all of them without stopping'). Merely mentioning 'loop' or 'until' in passing, in a quote, or in pasted text does not count.",
  scope_defined: "The request names the files, components, or area it applies to.",
  limits_defined:
    "The request states boundaries: what not to touch, a size or count limit, a time box, or constraints on the approach.",
  approach_defined: "The request says how the work should be done, not only what.",
  verification_defined:
    "The request states how success will be checked: tests to pass, expected output, or explicit acceptance criteria.",
  is_bug_report:
    "The message reports something that is broken or behaving wrongly (an error, a crash, a regression, wrong output, a failing test) and wants it fixed. A request for a new feature or a refactor is not a bug report.",
  touches_ui:
    "Fulfilling the request would change what a user sees or interacts with on screen: a page, component, layout, style, copy or navigation, in a web app or a mobile app.",
  needs_research:
    "Fulfilling the request first requires investigating or reading code or documentation to find something out (a cause, an option, where something lives) before any change can be made.",
};

const noulQuestions = Object.fromEntries(
  NOUL_AXES.map((axis) => [axis, { type: "noul", instructions: NOUL_INSTRUCTIONS[axis] }]),
) as Record<NoulAxis, JevQuestion>;

export const JEV_QUESTIONS: Record<AxisName, JevQuestion> = {
  ...noulQuestions,
  complexity: { type: "score", instructions: "How much work does fulfilling this request take?", criteria: [...COMPLEXITY_CRITERIA] },
  ambiguity: {
    type: "score",
    instructions: "How underspecified is the request, judged by whether scope, limits, and acceptance criteria are stated?",
    criteria: [...AMBIGUITY_CRITERIA],
  },
};

export function axisLabel(_axis: AxisName, value: AxisValue): string {
  return typeof value === "boolean" ? (value ? "yes" : "no") : value;
}

// The collapsed class scaffolds consume. Complexity matters only as "large";
// ambiguity only as "vague" ("partly" sits below the 0.5 line, see weights).
export function axisClass(axis: AxisName, value: AxisValue): string {
  if (axis === "complexity") return value === "large" ? "large" : "not-large";
  if (axis === "ambiguity") return value === "vague" ? "unclear" : "clear";
  return value === true ? "yes" : "no";
}

export function axisOutputs(axis: AxisName): readonly string[] {
  if (axis === "complexity") return COMPLEXITY_LEVELS;
  if (axis === "ambiguity") return AMBIGUITY_LEVELS;
  return ["yes", "no"];
}

export const SORT_QUESTION_PREFIX = "prompt-sort:";
export const sortQuestionName = (axis: AxisName): string => `${SORT_QUESTION_PREFIX}${axis}`;

// Applied to BOTH labels and predictions of these eval questions (RF-6):
// outcome labels are coarse, adjudicator labels are fine.
export const AXIS_COLLAPSE: Record<string, Record<string, string>> = {
  "prompt-sort:complexity": { trivial: "not-large", small: "not-large", substantial: "not-large", large: "large", "not-large": "not-large" },
  "prompt-sort:ambiguity": { clear: "clear", partly: "clear", vague: "unclear", unclear: "unclear" },
};

export function collapseLabel(question: string, label: string): string {
  return AXIS_COLLAPSE[question]?.[label] ?? label;
}

// The class that TRIGGERS a scaffold (C3): `brief` acts on verification_defined
// "no", so precision must be measured on that class, not on "yes".
export function positiveClass(axis: AxisName): string {
  if (axis === "complexity") return "large";
  if (axis === "ambiguity") return "unclear";
  if (axis === "verification_defined") return "no";
  return "yes";
}
