import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { profileScrubber } from "../../scope/commands.js";
import { Budget } from "../../scope/model.js";
import { audit } from "../audit.js";
import { clusterCorrections, correct, findCandidateTurns, labelTurns } from "../correct.js";
import { holdoutTitles } from "../corpus.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import { loadPrompt } from "../overlay.js";
import { classifyTier, renderSaved, saveProposal } from "../proposals.js";
import { loadRegistry } from "../registry.js";
import { parseSince, transcriptsDir } from "../transcripts.js";
import { isoWeek } from "../week.js";

const BUDGET_NEXT = "raise evolve.maxTokensPerJob in the profile (then sindri profile approve), or rerun later";

export async function correctCommand(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { since: { type: "string" }, json: { type: "boolean" } });
  const json = values.json === true;
  const since = parseSince(values.since, ctx.deps.now());
  const day = since.toISOString().slice(0, 10);
  const w = isoWeek(ctx.deps.now());
  const key = `${w.year}-W${String(w.week).padStart(2, "0")}`;
  // A complete run is remembered per ISO week and --since window, so a rerun (the weekly job rerun after
  // another step failed, or a second manual run) doesn't pay for the same labels again. Only that marker
  // says "already ran": a partial run saved some proposals but is meant to be rerun.
  const mark = `${key} ${values.since ?? "7d"}`;
  const ids = (ctx.db.prepare("SELECT id FROM proposals WHERE source = ? ORDER BY id").all(`correct:${key}`) as { id: string }[]).map((r) => r.id);
  const marked = ctx.db.prepare("SELECT 1 FROM evolve_audit WHERE verb = 'correct' AND detail = ? LIMIT 1").get(mark) !== undefined;
  if (marked) {
    const what = ids.length > 0 ? `: ${ids.join(", ")}` : " (nothing was proposed)";
    return success(`Already ran for ${key}${what}.\nNext: sindri evolve proposals`, { week: key, alreadyRan: true, ids }, json);
  }
  const registry = loadRegistry(ctx.db);
  if (registry.length === 0) throw new SindriError("SND-EVOLVE-010", "the artifact registry is empty");
  const scrubber = profileScrubber(ctx.loaded);
  const runner = ctx.io.runner(ctx.loaded, scrubber);
  const budget = new Budget(ctx.loaded.profile.evolve.maxTokensPerJob);
  const turns = findCandidateTurns(transcriptsDir(ctx), ctx.repo, since, holdoutTitles(ctx.deps), ctx.loaded.profile.evolve.maxCorrectTurns, scrubber);
  const labeled = await labelTurns(turns, { runner, model: ctx.loaded.profile.models.scoping, budget });
  const n = labeled.counts;
  const summary = `labeled ${labeled.labeled} turns: ${n.wrong_approach_design} design, ${n.wrong_approach_process} process, ${n.restate} restate, ${n.scope_surface} scope (${labeled.labelErrors} label errors)`;
  const stats = { turns: labeled.labeled, design: n.wrong_approach_design, process: n.wrong_approach_process, restate: n.restate, scope: n.scope_surface, labelErrors: labeled.labelErrors, labelDropped: labeled.labelDropped, incomplete: labeled.incomplete };
  const clusters = clusterCorrections(labeled.corrections);
  if (clusters.length === 0) {
    // Labeling finished cleanly and found nothing repeated: remember it. A cut-short or errored pass is not remembered, so it can be retried.
    if (turns.length > 0 && !labeled.incomplete && labeled.labelErrors === 0) await ctx.writeRetry((epoch) => audit(ctx.db, ctx.deps, "correct", mark, epoch, profileScrubber(ctx.loaded)));
    const partial = labeled.incomplete ? [`Partial result: ${labeled.notes.join("; ")}`] : [];
    const next = labeled.incomplete ? BUDGET_NEXT : "sindri evolve correct --since 30d";
    return success(
      [`No repeated corrections in sessions of ${ctx.repo} since ${day}: ${summary}.`, ...partial, `Next: ${next}`].join("\n"),
      { week: key, labeled: stats, clusters: 0 }, json, labeled.incomplete ? 1 : 0,
    );
  }
  const result = await correct({
    runner, model: ctx.loaded.profile.models.challenger, budget,
    prompt: loadPrompt(ctx.deps, "correct"), clusters, artifacts: registry,
  });
  const failed = labeled.labelErrors;
  const incomplete = labeled.incomplete || failed > 0 || result.incomplete;
  const notes = [...labeled.notes, ...(failed > 0 ? [`labeling: ${failed} failed batch(es)`] : []), ...result.notes];
  const extra = repoConfig(ctx.loaded).protectedPaths;
  const saved = await ctx.writeRetry((epoch) => {
    const out = result.proposals.map((p) => {
      const t = classifyTier(p, registry, extra);
      return { title: p.title, tier: t.tier, outcome: saveProposal(ctx.db, p, `correct:${key}`, t.tier, epoch, ctx.deps.now()) };
    });
    if (!incomplete) audit(ctx.db, ctx.deps, "correct", mark, epoch, profileScrubber(ctx.loaded));
    return out;
  });
  const noun = (c: number, one: string, many: string): string => `${c} ${c === 1 ? one : many}`;
  const { tiers, lines: savedLines } = renderSaved(saved);
  const head = `Correct: ${summary}; ${noun(clusters.length, "repeated-correction class", "repeated-correction classes")}, ${noun(saved.length, "proposal", "proposals")} (${tiers.length === 0 ? "0 code" : tiers.join(", ")}).`;
  const lines = [...savedLines, ...result.dropped.map((d) => `  dropped: ${d.title} (${d.why})`)];
  const fresh = saved.find((s) => s.outcome.kind === "saved");
  const partial = incomplete ? [`Partial result: ${notes.join("; ")}`] : [];
  const next = incomplete ? (labeled.incomplete ? BUDGET_NEXT : "rerun sindri evolve correct once the cause above is fixed") : fresh === undefined ? "sindri evolve proposals" : `sindri evolve show ${fresh.outcome.id}`;
  return success([head, ...lines, ...partial, `Next: ${next}`].join("\n"), { week: key, labeled: stats, clusters: clusters.length, proposals: saved, dropped: result.dropped, incomplete, notes }, json, incomplete ? 1 : 0);
}
