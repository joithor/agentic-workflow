// judge/src/prompt-sort/run.ts
// The whole `judge prompt-sort` path: skip -> sort (one Jev call) -> plan
// scaffolds -> record -> update session state -> print. Every storage or state
// failure is swallowed: the sorter must never be the reason a prompt breaks.
import crypto from "node:crypto";

import { z } from "zod";

import { resolvePromptSort, type JudgeConfig } from "../config.js";
import { recordFailure, type Db } from "../db.js";
import type { JevDeps } from "../providers/jev-api.js";
import { ALL_AXES, axisLabel } from "./axes.js";
import { NAMES_WORKFLOW, buildContext, planScaffolds } from "./scaffolds.js";
import { emptyState, readSortState, sortStatePath, writeSortState } from "./session-state.js";
import { sortPrompt, type SortOutcome } from "./sort.js";
import { recordPromptSort } from "./store.js";
import { tier1Skip } from "./tier1.js";

export const PromptSortInputSchema = z.object({ prompt: z.string(), sessionId: z.string().optional() });

export interface PromptSortDeps {
  db: Db; config: JudgeConfig; jev: JevDeps | null; stateDir: string;
  now?: () => Date; randomId?: () => string; clock?: () => number;
}
export interface CmdResult { exitCode: number; stdout: string; stderr?: string }

// RF-4: only a non-UI task follow-up the judge actually saw clears the requirement;
// a heuristic-only run (Jev outage) or a workflow-named prompt never does.
const clearsUiRequirement = (outcome: SortOutcome, namesWorkflow: boolean): boolean =>
  !namesWorkflow && outcome.mode === "blend" && outcome.values.is_task && !outcome.values.touches_ui &&
  outcome.axes.some((a) => a.axis === "touches_ui" && a.source === "judge");

const done = (body: unknown): CmdResult => ({ exitCode: 0, stdout: JSON.stringify(body) });

export async function runPromptSort(raw: unknown, deps: PromptSortDeps): Promise<CmdResult> {
  const parsed = PromptSortInputSchema.safeParse(raw);
  if (!parsed.success) return { exitCode: 1, stdout: "", stderr: "invalid prompt-sort input" };
  const { prompt } = parsed.data;
  const sessionId = parsed.data.sessionId === "" ? undefined : parsed.data.sessionId;

  const skip = tier1Skip(prompt);
  if (skip !== null) return done({ skipped: skip });
  const cfg = resolvePromptSort(deps.config);
  if (!cfg.enabled || deps.config.questions["prompt-sort"]?.enabled === false) return done({ skipped: "disabled" });

  const outcome = await sortPrompt(prompt, { jev: deps.jev, budgetMs: cfg.budgetMs, clock: deps.clock });

  const stateFile = sessionId === undefined ? null : sortStatePath(deps.stateDir, sessionId);
  const state = stateFile === null ? emptyState() : readSortState(stateFile);
  const promptIndex = state.prompts + 1;
  const plan = planScaffolds({
    values: outcome.values, axes: outcome.axes, switches: cfg.scaffolds, lastFired: state.lastFired,
    promptIndex, cooldownPrompts: cfg.cooldownPrompts, namesWorkflow: NAMES_WORKFLOW.test(prompt),
  });
  const fired = plan.fire.map((s) => s.id);
  const id = deps.randomId?.() ?? crypto.randomUUID();
  const ts = (deps.now?.() ?? new Date()).toISOString();

  try {
    recordPromptSort(deps.db, {
      id, ts, sessionId: sessionId ?? null, rawPrompt: prompt, outcome, wouldFire: plan.wouldFire, fired, suppressed: plan.suppressed,
    });
  } catch {
    // storage is best-effort: the answer below still stands
  }
  // C14: a Jev error or timeout is a failure `judge health` must see, even though
  // the decision itself stands (pure heuristics).
  if (outcome.failure !== null) {
    try {
      recordFailure(deps.db, { ts, question: "prompt-sort", provider: "jev", reason_code: outcome.failure });
    } catch {
      // nothing left to fall back to
    }
  }

  if (stateFile !== null) {
    try {
      const lastFired = { ...state.lastFired };
      for (const f of fired) lastFired[f] = promptIndex;
      let requirements = { ...state.requirements };
      if (plan.requirementUi) requirements = { uiEvidence: true };
      else if (clearsUiRequirement(outcome, NAMES_WORKFLOW.test(prompt))) requirements = {};
      writeSortState(stateFile, { prompts: promptIndex, lastFired, requirements });
    } catch {
      // a state file we cannot write only costs us the cooldown
    }
  }

  const axes = Object.fromEntries(ALL_AXES.map((a) => [a, axisLabel(a, outcome.values[a])]));
  return done({
    id, mode: outcome.mode, reason: outcome.reason, axes, fired, wouldFire: plan.wouldFire,
    suppressed: plan.suppressed, context: buildContext(plan.fire),
  });
}
