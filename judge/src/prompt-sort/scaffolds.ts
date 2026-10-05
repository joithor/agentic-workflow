// judge/src/prompt-sort/scaffolds.ts
// axes -> scaffolds. Pure. Each scaffold sits behind its own switch in
// config.json (promptSort.scaffolds); switches ship OFF (shadow mode), in which
// case the plan still reports what WOULD have fired for the eval.
//
// Design rules:
// - No generic skills. Prism's prism-route hook (~/.claude/settings.json
//   UserPromptSubmit, on_prompt.py) already does keyword skill routing. The one
//   workflow named here, /bugFixOrchestrator, is not in Prism's matcher.
// - A scaffold fires only when its DRIVING axis was decided by the judge. On a
//   heuristic-only run nothing fires: the heuristic is the baseline under test.
import type { AxisName, SortValues } from "./axes.js";
import type { AxisResult } from "./blend.js";

export const SCAFFOLD_IDS = ["brief", "bugfix", "ui-evidence", "plan-first"] as const;
export type ScaffoldId = (typeof SCAFFOLD_IDS)[number];
export type SuppressReason = "heuristic-only" | "switch-off" | "cooldown" | "loop-requested" | "already-named";

export interface Scaffold {
  id: ScaffoldId;
  needsJudge: readonly AxisName[];
  when: (v: SortValues) => boolean;
  text: string;
  requirement?: "uiEvidence";
}

export const SCAFFOLDS: readonly Scaffold[] = [
  {
    id: "brief",
    // Every axis the promotion rule measures for brief (SCAFFOLD_AXES, C2): acting
    // on a judged ambiguity but a heuristic verification/task read would fire on
    // a signal the eval never validated.
    needsJudge: ["ambiguity", "verification_defined", "is_task"],
    when: (v) => v.is_task && v.ambiguity === "vague" && !v.verification_defined && v.complexity !== "trivial",
    text: "Before editing, state in two lines the Goal and how you will Verify it (a test, a command's output, or a screenshot). If you cannot say how to verify it, ask one question first.",
  },
  {
    id: "bugfix",
    needsJudge: ["is_bug_report"],
    when: (v) => v.is_bug_report,
    text: "This reads as a bug report. /bugFixOrchestrator drives a bug to a fix proven by the check that failed before it; otherwise reproduce the failure before you change code.",
  },
  {
    id: "ui-evidence",
    needsJudge: ["touches_ui"],
    when: (v) => v.is_task && v.touches_ui,
    text: "This changes what users see. Before you say it is done, show UI evidence (a screenshot, a Playwright run or an iOS snapshot); the done gate will ask for it.",
    requirement: "uiEvidence",
  },
  {
    id: "plan-first",
    needsJudge: ["complexity"],
    when: (v) => v.is_task && v.complexity === "large",
    text: "This looks large. Outline a short plan first (files to touch, order, how each step is verified) and confirm it before a long run.",
  },
];

// A prompt that already names a workflow needs no pointer to it.
export const NAMES_WORKFLOW = /\b(bugFixOrchestrator|rootCause|bugHunt)\b/i;

export interface PlanInput {
  values: SortValues;
  axes: readonly AxisResult[];
  switches: Record<ScaffoldId, boolean>;
  lastFired: Partial<Record<ScaffoldId, number>>;
  promptIndex: number;
  cooldownPrompts: number;
  namesWorkflow: boolean;
}
export interface PlanOutput {
  wouldFire: ScaffoldId[];
  fire: Scaffold[];
  suppressed: Array<{ id: ScaffoldId; reason: SuppressReason }>;
  requirementUi: boolean;
}

export function planScaffolds(input: PlanInput): PlanOutput {
  const judged = new Set(input.axes.filter((a) => a.source === "judge").map((a) => a.axis));
  const out: PlanOutput = { wouldFire: [], fire: [], suppressed: [], requirementUi: false };
  for (const s of SCAFFOLDS) {
    if (!s.when(input.values)) continue;
    if (!s.needsJudge.every((axis) => judged.has(axis))) {
      out.suppressed.push({ id: s.id, reason: "heuristic-only" });
      continue;
    }
    out.wouldFire.push(s.id);
    if (!input.switches[s.id]) {
      out.suppressed.push({ id: s.id, reason: "switch-off" });
      continue;
    }
    // A prompt that names a workflow is never scaffolded (RF-3, C8).
    if (input.namesWorkflow) {
      out.suppressed.push({ id: s.id, reason: "already-named" });
      continue;
    }
    // The done-gate requirement outlives the text's cooldown (it is set below, before the cooldown check).
    // It does not apply to workflow-named prompts: those return above, so they never set it.
    if (s.requirement === "uiEvidence") out.requirementUi = true;
    if (input.values.wants_loop && (s.id === "brief" || s.id === "plan-first")) {
      out.suppressed.push({ id: s.id, reason: "loop-requested" });
      continue;
    }
    const last = input.lastFired[s.id];
    if (last !== undefined && input.promptIndex - last < input.cooldownPrompts) {
      out.suppressed.push({ id: s.id, reason: "cooldown" });
      continue;
    }
    out.fire.push(s);
  }
  return out;
}

const HEADER = "Prompt sorter (automatic, may be wrong; skip any note that does not fit):";

export function buildContext(fire: readonly Scaffold[]): string {
  if (fire.length === 0) return "";
  return [HEADER, ...fire.map((s) => `- ${s.text}`)].join("\n");
}
