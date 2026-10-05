// judge/src/prompt-sort/eval-provider.ts
// Eval-only Provider: answers "prompt-sort:<axis>" questions from the sorter
// itself, so `judge eval` can score heuristic vs blend vs judge-only on labeled
// prompts. All axes of one prompt share one sortPrompt run (one Jev request).
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { ALL_AXES, SORT_QUESTION_PREFIX, axisLabel, type AxisName } from "./axes.js";
import { SortEvalInputSchema } from "./eval-questions.js";
import { sortPrompt, type SortDeps, type SortOutcome } from "./sort.js";

export function makeSortEvalProvider(deps: SortDeps): Provider {
  const cache = new Map<string, Promise<SortOutcome>>();
  return {
    name: "jev",
    classes: new Set(["brief"]),
    decide: async <O extends string>(question: QuestionRef<O>, input: unknown): Promise<ProviderResult<O>> => {
      const axis = question.name.startsWith(SORT_QUESTION_PREFIX) ? question.name.slice(SORT_QUESTION_PREFIX.length) : "";
      if (!(ALL_AXES as readonly string[]).includes(axis)) return { status: "error", reason_code: "not-a-sort-question" };
      const parsed = SortEvalInputSchema.safeParse(input);
      if (!parsed.success) return { status: "error", reason_code: "invalid-input" };
      const mode = parsed.data.mode ?? "blend";
      const key = `${mode}\u0000${parsed.data.prompt}`;
      let run = cache.get(key);
      if (run === undefined) {
        run = sortPrompt(parsed.data.prompt, deps, mode);
        cache.set(key, run);
      }
      const outcome = await run;
      // C9: a Jev outage must not silently score heuristics as the blend. A judge
      // that answered but decided nothing ("all-undecided") is a real blend result.
      if (mode !== "heuristic" && outcome.mode === "heuristic-only" && outcome.reason !== "all-undecided") {
        return { status: "unavailable", reason_code: "jev-unavailable" };
      }
      const result = outcome.axes.find((a) => a.axis === axis) as NonNullable<SortOutcome["axes"][number]>;
      if (mode === "judge-only" && result.source !== "judge") return { status: "unavailable", reason_code: "undecided" };
      return { status: "decided", decision: axisLabel(axis as AxisName, result.value) as O, confidence: 1, reason_code: mode };
    },
  };
}
