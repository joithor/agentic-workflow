import type { LabelName } from "./labels.js";
import { labelerText, LABELS } from "./labels.js";
import type { PatternName } from "./patterns.js";
import { PATTERNS } from "./patterns.js";
import type { Interval } from "./stats.js";
import { wilson } from "./stats.js";

export interface LabeledTurn {
  key: string;
  text: string;
  editsBefore: boolean;
  labels: readonly LabelName[];
}

// Patterns with a counterpart in the taxonomy. The others (ci_conflicts, push_only, evidence_env,
// dispatch) have no label to check against, so they stay floor counts.
export const PATTERN_LABEL: Readonly<Partial<Record<PatternName, LabelName>>> = {
  restate: "restate",
  rigor: "rigor",
  scope_surface: "scope_surface",
  ship_recipe: "ship_recipe",
  handoff: "handoff",
  image_turn: "defect_report",
};
const CALIBRATED: readonly PatternName[] = ["restate", "rigor", "scope_surface", "ship_recipe", "handoff", "image_turn"];

export const MIN_LOWER_BOUND = 0.6;
export const MIN_POSITIVES = 10;
export const DESIGN_CORRECTIONS_PER_30_DAYS = 8; // spec §13 step 0 decision rule

export interface PatternCalibration {
  pattern: PatternName;
  label: LabelName;
  positives: number;
  tp: number;
  fp: number;
  fn: number;
  precision: Interval;
  recall: Interval;
  status: "metric-grade" | "floor only";
}

export function calibrate(labeled: readonly LabeledTurn[]): PatternCalibration[] {
  return CALIBRATED.map((pattern) => {
    const label = PATTERN_LABEL[pattern] as LabelName;
    let tp = 0;
    let fp = 0;
    let fn = 0;
    for (const t of labeled) {
      const predicted = PATTERNS[pattern].test(labelerText(t.text));
      const actual = t.labels.includes(label);
      if (predicted && actual) tp += 1;
      else if (predicted) fp += 1;
      else if (actual) fn += 1;
    }
    const precision = wilson(tp, tp + fp);
    const recall = wilson(tp, tp + fn);
    const positives = tp + fn;
    const grade = precision.lower >= MIN_LOWER_BOUND && recall.lower >= MIN_LOWER_BOUND && positives >= MIN_POSITIVES;
    return { pattern, label, positives, tp, fp, fn, precision, recall, status: grade ? "metric-grade" : "floor only" };
  });
}

export interface RateEstimate {
  inSample: number;
  rate: Interval;
  estimated: number;
  estimatedLower: number;
  estimatedUpper: number;
  per30Days: number;
  per30Lower: number;
  per30Upper: number;
}
export interface WrongApproachKind {
  all: RateEstimate;
  afterCode: RateEstimate;
}
export interface WrongApproachEstimate {
  sampled: number;
  totalTurns: number;
  windowDays: number;
  design: WrongApproachKind;
  process: WrongApproachKind;
  decision: "not-a-deliverable" | "deliverable" | "no-data";
  straddlesThreshold: boolean;
}

// The rate is the share of ALL sampled turns, so the estimate scales to all deduped typed turns.
function rateEstimate(k: number, n: number, totalTurns: number, windowDays: number): RateEstimate {
  const rate = wilson(k, n);
  const toTotal = (r: number): number => r * totalTurns;
  const toPer30 = (r: number): number => (windowDays > 0 ? (r * totalTurns * 30) / windowDays : 0);
  return {
    inSample: k,
    rate,
    estimated: toTotal(rate.rate),
    estimatedLower: toTotal(rate.lower),
    estimatedUpper: toTotal(rate.upper),
    per30Days: toPer30(rate.rate),
    per30Lower: toPer30(rate.lower),
    per30Upper: toPer30(rate.upper),
  };
}

export function estimateWrongApproach(labeled: readonly LabeledTurn[], totalTurns: number, windowDays: number): WrongApproachEstimate {
  const n = labeled.length;
  const kind = (label: LabelName): WrongApproachKind => {
    const hits = labeled.filter((t) => t.labels.includes(label));
    return {
      all: rateEstimate(hits.length, n, totalTurns, windowDays),
      afterCode: rateEstimate(hits.filter((t) => t.editsBefore).length, n, totalTurns, windowDays),
    };
  };
  const design = kind("wrong_approach_design");
  const after = design.afterCode;
  const decision = n === 0 ? "no-data" : after.per30Days < DESIGN_CORRECTIONS_PER_30_DAYS ? "not-a-deliverable" : "deliverable";
  return {
    sampled: n,
    totalTurns,
    windowDays,
    design,
    process: kind("wrong_approach_process"),
    decision,
    straddlesThreshold: n > 0 && after.per30Lower < DESIGN_CORRECTIONS_PER_30_DAYS && after.per30Upper >= DESIGN_CORRECTIONS_PER_30_DAYS,
  };
}

// Raw agreement of label presence between two labeling passes over the same turns.
export function repeatAgreement(
  first: ReadonlyMap<string, readonly LabelName[]>,
  second: ReadonlyMap<string, readonly LabelName[]>,
): { compared: number; agreement: Record<LabelName, number | null> } {
  const ids = [...second.keys()].filter((id) => first.has(id));
  const agreement = {} as Record<LabelName, number | null>;
  for (const label of LABELS) {
    if (ids.length === 0) {
      agreement[label] = null;
      continue;
    }
    const same = ids.filter((id) => (first.get(id) as readonly LabelName[]).includes(label) === (second.get(id) as readonly LabelName[]).includes(label)).length;
    agreement[label] = same / ids.length;
  }
  return { compared: ids.length, agreement };
}
