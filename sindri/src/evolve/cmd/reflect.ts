import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { profileScrubber } from "../../scope/commands.js";
import { Budget } from "../../scope/model.js";
import { auditMarker } from "../audit.js";
import { holdoutTitles } from "../corpus.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import { allowedAuthors, ghRepoOf, prContext } from "../github.js";
import { loadPrompt } from "../overlay.js";
import { classifyTier, renderSaved, saveProposal } from "../proposals.js";
import { branchTranscript, reflect } from "../reflect.js";
import { loadRegistry } from "../registry.js";
import { transcriptsDir } from "../transcripts.js";

export function reflectedBefore(ctx: EvolveCtx, pr: number): { reflected: boolean; ids: string[] } {
  const ids = (ctx.db.prepare("SELECT id FROM proposals WHERE source = ? ORDER BY id").all(`reflect:pr-${pr}`) as { id: string }[]).map((r) => r.id);
  const marked = ctx.db.prepare("SELECT 1 FROM evolve_audit WHERE verb = 'reflect' AND detail = ? LIMIT 1").get(`pr-${pr}`) !== undefined;
  return { reflected: marked, ids }; // the marker is written only by a complete run, so a partial run can be rerun
}

// `budget` lets a caller (the weekly job) share one budget across several PRs; alone, each PR gets its own.
export async function reflectCommand(args: string[], ctx: EvolveCtx, budget: Budget = new Budget(ctx.loaded.profile.evolve.maxTokensPerJob)): Promise<CommandResult> {
  const { values } = parseFlags(args, { pr: { type: "string" }, json: { type: "boolean" } });
  const json = values.json === true;
  if (values.pr === undefined || !/^[1-9]\d{0,6}$/.test(values.pr)) throw new SindriError("SND-CLI-002", "usage: sindri evolve reflect --pr <n>");
  const pr = Number(values.pr);
  const before = reflectedBefore(ctx, pr);
  if (before.reflected) {
    const what = before.ids.length > 0 ? `: ${before.ids.join(", ")}` : " (nothing was proposed)";
    return success(`Already reflected on PR #${pr}${what}.\nNext: sindri evolve proposals`, { pr, alreadyReflected: true, ids: before.ids }, json);
  }
  const registry = loadRegistry(ctx.db);
  if (registry.length === 0) throw new SindriError("SND-EVOLVE-010", "the artifact registry is empty");
  const ghRepo = await ghRepoOf(ctx.deps.git, ctx.repo);
  const allowed = await allowedAuthors(ctx.io.process, ctx.repo, ctx.loaded.profile.evolve.prAuthors);
  const view = await prContext(ctx.io.process, ctx.repo, ghRepo, pr, allowed);
  const transcript = branchTranscript(transcriptsDir(ctx), ctx.repo, view.branch, { cap: 60_000, since: new Date(ctx.deps.now().getTime() - 60 * 86_400_000), dropTitles: holdoutTitles(ctx.deps) });
  const result = await reflect({
    runner: ctx.io.runner(ctx.loaded, profileScrubber(ctx.loaded)),
    models: { reviewer: ctx.loaded.profile.models.scoping, synthesizer: ctx.loaded.profile.models.challenger },
    budget,
    prompts: {
      judgment: loadPrompt(ctx.deps, "reflect.judgment"), tooling: loadPrompt(ctx.deps, "reflect.tooling"),
      divergent: loadPrompt(ctx.deps, "reflect.divergent"), synthesize: loadPrompt(ctx.deps, "reflect.synthesize"),
    },
    pr: view, transcript, artifacts: registry,
  });
  const extra = repoConfig(ctx.loaded).protectedPaths;
  const saved = await ctx.writeRetry((epoch) => {
    const out = result.accepted.map((p) => {
      const t = classifyTier(p, registry, extra);
      return { title: p.title, tier: t.tier, outcome: saveProposal(ctx.db, p, `reflect:pr-${pr}`, t.tier, epoch, ctx.deps.now()) };
    });
    if (!result.incomplete) auditMarker(ctx.db, ctx.deps, "reflect", `pr-${pr}`, epoch);
    return out;
  });
  const { tiers, lines } = renderSaved(saved);
  const head = `Reflected on PR #${pr}: ${saved.length} accepted${tiers.length === 0 ? "" : ` (${tiers.join(", ")})`}, ${result.rejected.length} rejected, ${result.backlog.length} backlog.`;
  const fresh = saved.find((s) => s.outcome.kind === "saved");
  const partial = result.incomplete ? [`Partial result: ${result.notes.join("; ")}`] : [];
  const next = result.incomplete ? `rerun sindri evolve reflect --pr ${pr} once the cause above is fixed` : fresh === undefined ? "sindri evolve proposals" : `sindri evolve show ${fresh.outcome.id}`;
  return success([head, ...lines, ...partial, `Next: ${next}`].join("\n"), { pr, accepted: saved, rejected: result.rejected, backlog: result.backlog, incomplete: result.incomplete, notes: result.notes }, json, result.incomplete ? 1 : 0);
}
