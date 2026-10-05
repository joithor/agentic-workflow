// judge/src/prompt-sort/blend.ts
// Decisive-band policy (Navigator hooks/nav_hook_lib/judge.py): per axis, the
// judge counts only when decisive; otherwise the keyword heuristic stays in
// charge for that axis. Never a whole-call failure (RF-2).
import type { JevAnswer, JevQuestionResult } from "../providers/jev-api.js";
import {
  ALL_AXES, AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS, BAND, COMPLEXITY_CRITERIA, COMPLEXITY_LEVELS, NOUL_AXES, axisClass,
  type AxisName, type AxisValue, type SortValues,
} from "./axes.js";

export interface JudgeRead { noul?: number; level?: string; value?: number; confidence?: number }
export type JudgeReads = Partial<Record<AxisName, JudgeRead>>;
export type AxisStatus = "agreed" | "overrode" | "undecided";
export interface AxisResult {
  axis: AxisName; value: AxisValue; heuristic: AxisValue; source: "judge" | "heuristic";
  status: AxisStatus; probability: number | null; confidence: number | null;
}

// Jev's probabilities may be keyed by index ("0"), by short level name, or by the
// full "name: description" criterion we sent; accept all three.
function probFor(probs: Record<string, number>, index: number, criterion: string): number {
  return probs[String(index)] ?? probs[criterion] ?? probs[criterion.split(":")[0] as string] ?? 0;
}

export function scoreRead(
  a: Extract<JevAnswer, { type: "score" }>, criteria: readonly string[], weights?: readonly number[],
): { index: number; value: number; confidence: number } {
  const top = criteria.length - 1;
  const probs = criteria.map((c, i) => probFor(a.probabilities, i, c));
  const mass = probs.reduce((s, p) => s + p, 0);
  const index = mass > 0 ? probs.indexOf(Math.max(...probs)) : Math.min(top, Math.max(0, Math.round(a.score)));
  let value: number;
  if (weights === undefined) value = index / top;
  else if (mass > 0) value = probs.reduce((s, p, i) => s + p * (weights[i] as number), 0) / mass;
  else value = weights[index] as number;
  return { index, value, confidence: a.confidence };
}

const ambiguityLevel = (value: number): string => (value >= 0.5 ? "vague" : value >= 0.2 ? "partly" : "clear");

export function readJudge(answers: Record<string, JevQuestionResult>): JudgeReads {
  const reads: JudgeReads = {};
  for (const axis of NOUL_AXES) {
    const r = answers[axis];
    if (r?.ok === true && r.answer.type === "noul") reads[axis] = { noul: r.answer.noul };
  }
  const c = answers.complexity;
  if (c?.ok === true && c.answer.type === "score") {
    const s = scoreRead(c.answer, COMPLEXITY_CRITERIA);
    reads.complexity = { level: COMPLEXITY_LEVELS[s.index] as string, value: s.value, confidence: s.confidence };
  }
  const a = answers.ambiguity;
  if (a?.ok === true && a.answer.type === "score") {
    const s = scoreRead(a.answer, AMBIGUITY_CRITERIA, AMBIGUITY_WEIGHTS);
    reads.ambiguity = { level: ambiguityLevel(s.value), value: s.value, confidence: s.confidence };
  }
  return reads;
}

function decisive(axis: AxisName, read: JudgeRead | undefined): AxisValue | null {
  if (read === undefined) return null;
  if (axis === "complexity" || axis === "ambiguity") {
    if (read.level === undefined || (read.confidence as number) < BAND.minConfidence) return null;
    return read.level as AxisValue;
  }
  if (read.noul === undefined) return null;
  if (read.noul >= BAND.high) return true;
  if (read.noul <= BAND.low) return false;
  return null;
}

export function blend(heur: SortValues, judge: JudgeReads | null): { values: SortValues; axes: AxisResult[]; judgeDecided: number } {
  const values: Record<AxisName, AxisValue> = { ...heur };
  const axes: AxisResult[] = [];
  let judgeDecided = 0;
  for (const axis of ALL_AXES) {
    const heuristic = heur[axis];
    const read = judge?.[axis];
    const probability = read?.noul ?? read?.value ?? null;
    const confidence = read?.confidence ?? null;
    const verdict = decisive(axis, read);
    if (verdict === null) {
      axes.push({ axis, value: heuristic, heuristic, source: "heuristic", status: "undecided", probability, confidence });
      continue;
    }
    values[axis] = verdict;
    judgeDecided++;
    const status: AxisStatus = axisClass(axis, verdict) === axisClass(axis, heuristic) ? "agreed" : "overrode";
    axes.push({ axis, value: verdict, heuristic, source: "judge", status, probability, confidence });
  }
  return { values: values as SortValues, axes, judgeDecided };
}
