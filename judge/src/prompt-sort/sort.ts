// judge/src/prompt-sort/sort.ts
// One batched Jev request per prompt (Plan A callJev), then the per-axis blend.
// The request carries the redacted, capped prompt only (RF-1). Every failure
// path returns pure heuristics, never throws (RF-2).
import { callJev, type JevDeps } from "../providers/jev-api.js";
import { redactSecrets } from "../redact.js";
import { JEV_QUESTIONS, type SortValues } from "./axes.js";
import { blend, readJudge, type AxisResult } from "./blend.js";
import { heuristicSort } from "./heuristics.js";

export const STATE_CAP = 4000;
export type SortMode = "blend" | "heuristic" | "judge-only";
export interface SortDeps { jev: JevDeps | null; budgetMs: number; clock?: () => number }
export interface SortOutcome {
  values: SortValues; axes: AxisResult[]; judgeDecided: number;
  mode: "blend" | "heuristic-only"; reason: string; failure: string | null; latencyMs: number;
  usage: { input_tokens: number; output_tokens: number } | null; sentPrompt: string;
}

export async function sortPrompt(prompt: string, deps: SortDeps, mode: SortMode = "blend"): Promise<SortOutcome> {
  const clock = deps.clock ?? Date.now;
  const start = clock();
  const heur = heuristicSort(prompt);
  const sentPrompt = redactSecrets(prompt).slice(0, STATE_CAP);
  const heuristicOnly = (reason: string, failure: string | null): SortOutcome => ({
    ...blend(heur, null), mode: "heuristic-only", reason, failure, latencyMs: clock() - start, usage: null, sentPrompt,
  });
  if (mode === "heuristic") return heuristicOnly("heuristic-mode", null);
  if (deps.jev === null) return heuristicOnly("no-jev", null);

  const out = await callJev(deps.jev, sentPrompt, JEV_QUESTIONS, deps.budgetMs);
  if (out.status !== "ok") {
    // A timeout or a real API error is a failure `judge health` should count; a
    // missing key or a dropped connection is just "no judge this time".
    return heuristicOnly(out.reason_code, out.status === "error" || out.reason_code === "timeout" ? out.reason_code : null);
  }
  const blended = blend(heur, readJudge(out.answers));
  return {
    ...blended,
    mode: blended.judgeDecided > 0 ? "blend" : "heuristic-only",
    reason: blended.judgeDecided > 0 ? "sorted" : "all-undecided",
    failure: null, latencyMs: clock() - start, usage: out.usage, sentPrompt,
  };
}
