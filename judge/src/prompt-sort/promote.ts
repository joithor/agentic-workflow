// judge/src/prompt-sort/promote.ts
// The go/no-go rule for turning a scaffold on. The numbers below are fixed in
// docs/superpowers/plans/2026-10-04-prompt-sorter.md BEFORE any results exist;
// do not edit them after looking at data. applyPromotion only ever turns
// switches ON; turning one off is a deliberate manual `judge prompt-sort scaffold`.
import type { AxisName } from "./axes.js";
import type { AxisEvalSummary, SortEvalResult } from "./eval-run.js";
import { runScaffoldSwitch } from "./scaffold-switch.js";
import { SCAFFOLD_IDS, type ScaffoldId } from "./scaffolds.js";

export const RULE = {
  minAdjudicator: 60, minOutcome: 40, minLift: 0.1, minAccuracyAdjudicator: 0.75,
  minAccuracyOutcome: 0.7, minPrecision: 0.7, minAgreement: 0.7, minShared: 20,
} as const;

export const SCAFFOLD_AXES: Record<ScaffoldId, readonly AxisName[]> = {
  brief: ["ambiguity", "verification_defined", "is_task"],
  bugfix: ["is_bug_report"],
  "ui-evidence": ["touches_ui"],
  "plan-first": ["complexity"],
};
// Axes with an outcome labeler (outcomes.ts), and the one whose outcome labels are positives only.
export const OUTCOME_AXES: ReadonlySet<AxisName> = new Set<AxisName>(["is_task", "is_bug_report", "touches_ui", "complexity", "ambiguity"]);
export const RECALL_ONLY_AXES: ReadonlySet<AxisName> = new Set<AxisName>(["is_bug_report"]);

export interface Verdict { scaffold: ScaffoldId; go: boolean; reasons: string[] }

const r2 = (x: number): string => String(Math.round(x * 100) / 100);

function checkSource(axis: AxisName, s: AxisEvalSummary | undefined, source: "adjudicator" | "outcome", reasons: string[]): void {
  const minN = source === "adjudicator" ? RULE.minAdjudicator : RULE.minOutcome;
  if (s === undefined) {
    reasons.push(`${axis}: no ${source} labels`);
    return;
  }
  if (s.n < minN) reasons.push(`${axis}: ${source} n=${s.n} < ${minN}`);
  const lift = Math.round((s.blend.accuracy - s.heuristic.accuracy) * 100) / 100;
  if (lift < RULE.minLift) reasons.push(`${axis}: ${source} lift ${r2(lift)} < ${RULE.minLift}`);
  const floor = source === "adjudicator" ? RULE.minAccuracyAdjudicator : RULE.minAccuracyOutcome;
  if (s.blend.accuracy < floor) reasons.push(`${axis}: ${source} accuracy ${r2(s.blend.accuracy)} < ${floor}`);
  if (source === "outcome" && RECALL_ONLY_AXES.has(axis)) return;
  if (s.blend.ppv === null) reasons.push(`${axis}: ${source} precision n/a`);
  else if (s.blend.ppv < RULE.minPrecision) reasons.push(`${axis}: ${source} precision ${r2(s.blend.ppv)} < ${RULE.minPrecision}`);
}

export function decidePromotion(result: SortEvalResult): Verdict[] {
  return SCAFFOLD_IDS.map((scaffold) => {
    const reasons: string[] = [];
    for (const axis of SCAFFOLD_AXES[scaffold]) {
      const find = (source: "adjudicator" | "outcome") => result.summaries.find((s) => s.axis === axis && s.source === source);
      checkSource(axis, find("adjudicator"), "adjudicator", reasons);
      if (!OUTCOME_AXES.has(axis)) continue;
      checkSource(axis, find("outcome"), "outcome", reasons);
      const a = result.agreement.find((x) => x.axis === axis) ?? { axis, shared: 0, agreed: 0 };
      if (a.shared < RULE.minShared) reasons.push(`${axis}: only ${a.shared} prompts labeled by both sources (< ${RULE.minShared})`);
      else if (a.agreed / a.shared < RULE.minAgreement) reasons.push(`${axis}: label sources agree ${r2(a.agreed / a.shared)} < ${RULE.minAgreement}`);
    }
    return { scaffold, go: reasons.length === 0, reasons };
  });
}

export function applyPromotion(configFile: string, verdicts: readonly Verdict[]): ScaffoldId[] {
  const turned: ScaffoldId[] = [];
  for (const v of verdicts) {
    if (!v.go) continue;
    const res = runScaffoldSwitch(v.scaffold, "on", configFile);
    if (res.exitCode === 0) turned.push(v.scaffold);
  }
  return turned;
}
