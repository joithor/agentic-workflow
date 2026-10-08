import fs from "node:fs";
import path from "node:path";

import type { LabeledTurn, PatternCalibration, WrongApproachEstimate, WrongApproachKind } from "./calibrate.js";
import { calibrate, DESIGN_CORRECTIONS_PER_30_DAYS, estimateWrongApproach, repeatAgreement } from "./calibrate.js";
import type { HumanTurn } from "./human-turns.js";
import type { LabelItem, LabelName, LabelRunner } from "./labels.js";
import { labelItems, LABELS, sampleTurns, turnKey } from "./labels.js";
import type { Interval } from "./stats.js";

export interface LabelOptions {
  n: number;
  repeat: number;
  model: string;
  runner: LabelRunner;
  windowDays: number; // nominal --since window; runLabeling caps it at the span the turns actually cover
}

export interface LabelingReport {
  model: string;
  requested: number;
  sampled: number;
  labeled: number;
  labelErrors: number;
  calibration: PatternCalibration[];
  wrongApproach: WrongApproachEstimate;
  repeat: { requested: number; compared: number; agreement: Record<LabelName, number | null> };
}

// totalTurns is the number of deduped typed turns, so sample rates scale to the whole corpus.
// opts.windowDays is the nominal --since window. Transcripts are pruned (Claude Code keeps about 30 days by
// default), so --since 90d can reach past the oldest surviving turn; dividing by the nominal window would
// understate the per-30-day rate and push the decision rule toward "not-a-deliverable". Use the span the turns
// actually cover, capped at the nominal window and floored at one day.
export function observedWindowDays(turns: readonly HumanTurn[], nominalDays: number): number {
  let min = Infinity;
  let max = -Infinity;
  for (const t of turns) {
    const ms = Date.parse(t.ts);
    if (!Number.isFinite(ms)) continue;
    if (ms < min) min = ms;
    if (ms > max) max = ms;
  }
  if (min === Infinity) return nominalDays;
  return Math.min(nominalDays, Math.max(1, (max - min) / 86_400_000));
}

export async function runLabeling(turns: readonly HumanTurn[], totalTurns: number, opts: LabelOptions, outDir: string): Promise<LabelingReport> {
  const windowDays = observedWindowDays(turns, opts.windowDays);
  const sample = sampleTurns(turns, opts.n);
  const items: LabelItem[] = sample.map((t, i) => ({ id: `t${i}`, prevAssistantTail: t.prevAssistantTail, text: t.text }));
  const first = await labelItems(items, opts.runner);
  // Second pass over the first k sampled turns, batches in reverse order, to measure labeler stability.
  const repeatItems = items.slice(0, Math.min(opts.repeat, items.length)).reverse();
  const second = repeatItems.length > 0 ? await labelItems(repeatItems, opts.runner) : { labels: new Map<string, LabelName[]>(), labelErrors: 0 };

  const labeled: LabeledTurn[] = [];
  const lines: string[] = [];
  sample.forEach((t, i) => {
    const labels = first.labels.get(`t${i}`);
    if (labels === undefined) return;
    labeled.push({ key: turnKey(t), text: t.text, editsBefore: t.editsBefore, labels });
    lines.push(JSON.stringify({ key: turnKey(t), labels, model: opts.model, pass: 1 }));
  });
  sample.forEach((t, i) => {
    const labels = second.labels.get(`t${i}`);
    if (labels !== undefined) lines.push(JSON.stringify({ key: turnKey(t), labels, model: opts.model, pass: 2 }));
  });

  const report: LabelingReport = {
    model: opts.model,
    requested: opts.n,
    sampled: sample.length,
    labeled: labeled.length,
    labelErrors: first.labelErrors + second.labelErrors,
    calibration: calibrate(labeled),
    wrongApproach: estimateWrongApproach(labeled, totalTurns, windowDays),
    repeat: { requested: repeatItems.length, ...repeatAgreement(first.labels, second.labels) },
  };
  fs.mkdirSync(outDir, { recursive: true });
  // labels.jsonl holds keys and labels only (no turn text); it still stays local and is never committed.
  const labelsFile = path.join(outDir, "labels.jsonl");
  if (fs.existsSync(labelsFile)) fs.chmodSync(labelsFile, 0o600); // writeFileSync's mode only applies on create: tighten before any write
  fs.writeFileSync(labelsFile, lines.length === 0 ? "" : `${lines.join("\n")}\n`, { mode: 0o600 });
  fs.writeFileSync(path.join(outDir, "calibration.json"), `${JSON.stringify(report, null, 2)}\n`);
  return report;
}

const pct = (x: number, digits = 0): string => `${(100 * x).toFixed(digits)}%`;
const withLower = (i: Interval): string => `${pct(i.rate)} (lower ${pct(i.lower)})`;

function wrongApproachRow(name: string, k: WrongApproachKind): string {
  const a = k.all;
  const c = k.afterCode;
  return `| ${name} | ${a.inSample} | ${pct(a.rate.rate, 1)} (${pct(a.rate.lower, 1)} to ${pct(a.rate.upper, 1)}) | ${Math.round(a.estimated)} | ${c.inSample} | ${Math.round(c.estimated)} | ${c.per30Days.toFixed(1)} (${c.per30Lower.toFixed(1)} to ${c.per30Upper.toFixed(1)}) |`;
}

// Aggregate numbers only: nothing here quotes a turn.
export function renderLabeling(report: LabelingReport | null): string[] {
  if (report === null) return ["Patterns are uncalibrated floor counts; run with --label 400 to calibrate.", ""];
  const wa = report.wrongApproach;
  const agreement = LABELS.map((l) => `${l} ${report.repeat.agreement[l] === null ? "n/a" : pct(report.repeat.agreement[l] as number)}`).join(", ");
  const verdict =
    wa.decision === "no-data"
      ? "No labeled turns, so no decision."
      : wa.decision === "not-a-deliverable"
        ? `Design corrections after code average ${wa.design.afterCode.per30Days.toFixed(1)} per 30 days (under ${DESIGN_CORRECTIONS_PER_30_DAYS}): Approach and Drift direction checks are not a step-3a deliverable (not built, not even in shadow).`
        : `Design corrections after code average ${wa.design.afterCode.per30Days.toFixed(1)} per 30 days (at least ${DESIGN_CORRECTIONS_PER_30_DAYS}): Approach and Drift direction checks stay a step-3a deliverable.`;
  return [
    "## Pattern calibration",
    "",
    `Labeled ${report.labeled} of ${report.sampled} sampled turns with ${report.model} (${report.labelErrors} failed batches). A pattern is metric-grade only when precision and recall both have a 95% Wilson lower bound of at least 0.6 and the label has at least 10 positives; the rest are floor counts. Precision and recall measure agreement with the model labeler, not ground truth.`,
    "",
    "| pattern | label | positives | TP | FP | FN | precision | recall | status |",
    "|---|---|---|---|---|---|---|---|---|",
    ...report.calibration.map((c) => `| ${c.pattern} | ${c.label} | ${c.positives} | ${c.tp} | ${c.fp} | ${c.fn} | ${withLower(c.precision)} | ${withLower(c.recall)} | ${c.status} |`),
    "",
    `Patterns without a label (ci_conflicts, push_only, evidence_env, dispatch) stay floor counts.`,
    `Labeler stability: ${report.repeat.compared} turns relabeled in reverse batch order. Raw agreement per label: ${agreement}.`,
    "",
    "## Wrong-approach corrections",
    "",
    `Scaled from ${wa.sampled} labeled turns to ${wa.totalTurns} deduped typed turns over ${wa.windowDays.toFixed(0)} days. After code means an earlier edit tool call in the same session.`,
    "",
    "| kind | in sample | share of turns (95% CI) | est. all turns | after code: in sample | after code: est. | after code: per 30 days (95% CI) |",
    "|---|---|---|---|---|---|---|",
    wrongApproachRow("design", wa.design),
    wrongApproachRow("process", wa.process),
    "",
    `${verdict}${wa.straddlesThreshold ? ` The 95% interval straddles ${DESIGN_CORRECTIONS_PER_30_DAYS}, so this decision is provisional: re-run with a larger --label (a value above the turn count labels every typed turn) before acting, and re-check monthly.` : ""}`,
    "",
  ];
}
