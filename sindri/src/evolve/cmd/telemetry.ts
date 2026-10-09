import fs from "node:fs";

import { parseFlags } from "../../args.js";
import { success, type CommandResult } from "../../output.js";
import { profileScrubber } from "../../scope/commands.js";
import { Budget } from "../../scope/model.js";
import { positiveInt, repoConfig, type EvolveCtx } from "../ctx.js";
import { classifyTier, saveProposal } from "../proposals.js";
import { loadRegistry } from "../registry.js";
import { wilsonLower } from "../stats.js";
import { adjudicateFires, findHookFires, hookFixProposal } from "../telemetry.js";
import { parseSince, transcriptsDir } from "../transcripts.js";

const BUDGET_WHY = "token budget exhausted";
const MIN_SAMPLES = 10;
const FP_BAR = 0.2;

export async function telemetry(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { since: { type: "string" }, "per-hook": { type: "string" }, json: { type: "boolean" } });
  const json = values.json === true;
  const since = parseSince(values.since, ctx.deps.now());
  const perHook = positiveInt(values["per-hook"], 20, "--per-hook");
  const day = since.toISOString().slice(0, 10);
  const dir = transcriptsDir(ctx);
  if (!fs.existsSync(dir)) {
    return success(`No transcripts directory at ${dir}.\nNext: set sources.transcripts.dir in the profile, then sindri profile approve`, { dir, hooks: [] }, json, 1);
  }
  const fires = findHookFires(dir, ctx.repo, since);
  // Only samples from after the latest merged hook-fix for a hook count: the fix is what they
  // measured, so pre-fix samples would re-propose it.
  const mergedAt = (hook: string): string | null =>
    (ctx.db.prepare("SELECT MAX(updated_at) AS at FROM proposals WHERE artifact_id = ? AND kind = 'hook-fix' AND status = 'merged'").get(`hook:${hook}`) as { at: string | null }).at;
  // A fire is sampled once: by ref, and by (hook, ts), because a session resumed or forked into a
  // file that is scanned first has the same fire under a new ref (Review Focus 9). The ledger keeps no
  // message text, so the timestamp (to the millisecond in real transcripts) stands in for it.
  // A NULL sample (an answer the adjudicator never labelled) is unknown, not sampled: it is retried.
  const samples = ctx.db.prepare("SELECT hook, ref, ts FROM hook_samples WHERE warranted IS NOT NULL").all() as { hook: string; ref: string; ts: string }[];
  const known = new Set(samples.flatMap((r) => [r.ref, `${r.hook}\u0000${r.ts}`]));
  const fresh = fires.filter((f) => !known.has(f.ref) && !known.has(`${f.hook}\u0000${f.ts}`) && f.ts > (mergedAt(f.hook) ?? ""));
  let stopped: { skipped: number; why: string } | null = null;
  let dropped = 0;
  if (fresh.length > 0) {
    const adj = await adjudicateFires(fresh, {
      runner: ctx.io.runner(ctx.loaded, profileScrubber(ctx.loaded)), model: ctx.loaded.profile.models.adjudicator, budget: new Budget(ctx.loaded.profile.evolve.maxTokensPerJob), perHook,
    });
    await ctx.writeRetry((epoch) => {
      ctx.db.prepare("DELETE FROM hook_samples WHERE warranted IS NULL").run(); // left by earlier runs; the labels below replace them
      for (const l of adj.labels) {
        if (l.warranted === null) continue; // dropped: stays unknown, so the next run adjudicates it again
        ctx.db.prepare("INSERT OR IGNORE INTO hook_samples (hook, ref, ts, warranted, reason, sampled_at, epoch) VALUES (?, ?, ?, ?, ?, ?, ?)")
          .run(l.hook, l.ref, l.ts, l.warranted ? 1 : 0, l.reason, ctx.deps.now().toISOString(), epoch);
      }
    });
    dropped = adj.dropped;
    if (adj.incomplete) stopped = { skipped: adj.skipped, why: adj.why };
  }

  const registry = loadRegistry(ctx.db);
  const all = ctx.db.prepare("SELECT hook, ts, warranted FROM hook_samples WHERE warranted IS NOT NULL").all() as { hook: string; ts: string; warranted: number }[];
  const stats = [...new Set(all.map((r) => r.hook))].sort().map((hook) => {
    const at = mergedAt(hook);
    const rows = all.filter((r) => r.hook === hook && (at === null || r.ts > at));
    return { hook, labelled: rows.length, unwarranted: rows.filter((r) => r.warranted === 0).length, since: at };
  });
  const hooks = [...new Set([...stats.map((s) => s.hook), ...fires.map((f) => f.hook)])].sort();
  if (hooks.length === 0) {
    return success(`No hook fires found in sessions of ${ctx.repo} since ${day}.\nNext: sindri evolve telemetry --since 30d`, { since: day, hooks: [] }, json);
  }

  const lines: string[] = [];
  const rows: { hook: string; fires: number; labelled: number; unwarranted: number; rate: number | null; lower: number | null }[] = [];
  let firstOpened: string | null = null;
  for (const hook of hooks) {
    const st = stats.find((s) => s.hook === hook) ?? { hook, labelled: 0, unwarranted: 0, since: null };
    const count = fires.filter((f) => f.hook === hook).length;
    const enough = st.labelled >= MIN_SAMPLES;
    const rate = enough ? st.unwarranted / st.labelled : null;
    const lower = enough ? wilsonLower(st.unwarranted, st.labelled) : null;
    rows.push({ hook, fires: count, labelled: st.labelled, unwarranted: st.unwarranted, rate, lower });
    const tail = rate === null || lower === null ? `not enough samples yet (${st.labelled}/${MIN_SAMPLES})` : `FP rate ${rate.toFixed(2)} (lower bound ${lower.toFixed(2)})`;
    lines.push(`${hook}: ${count} fire(s) since ${day}; ${st.labelled} labelled sample(s)${st.since === null ? "" : ` after the merged fix of ${st.since.slice(0, 10)}`}, ${st.unwarranted} unwarranted; ${tail}`);
    if (lower === null || lower <= FP_BAR) continue;
    const artifact = registry.find((a) => a.id === `hook:${hook}`);
    if (artifact === undefined) {
      lines.push(`  no registered artifact hook:${hook}; run sindri evolve init`);
      continue;
    }
    const refs = (ctx.db.prepare("SELECT ref FROM hook_samples WHERE hook = ? AND warranted = 0 AND ts > ? ORDER BY ts DESC LIMIT 5").all(hook, st.since ?? "") as { ref: string }[]).map((r) => r.ref);
    const proposal = hookFixProposal(artifact, st, lower, refs);
    const tier = classifyTier(proposal, registry, repoConfig(ctx.loaded).protectedPaths).tier;
    const saved = await ctx.writeRetry((epoch) => saveProposal(ctx.db, proposal, `telemetry:${hook}`, tier, epoch, ctx.deps.now()));
    lines.push(saved.kind === "saved" ? `  opened proposal ${saved.id} (${tier})` : `  already proposed (${saved.id})`);
    if (saved.kind === "saved") firstOpened ??= saved.id;
  }
  if (dropped > 0) lines.push(`${dropped} adjudicator answer(s) were malformed and dropped; those fires stay unlabelled and are adjudicated again on the next run.`);
  if (stopped !== null) lines.push(`Stopped early: ${stopped.why}; ${stopped.skipped} fire(s) were not adjudicated.`);
  const next = stopped !== null
    ? stopped.why === BUDGET_WHY
      ? "raise evolve.maxTokensPerJob in the profile (then sindri profile approve), or rerun later"
      : "rerun sindri evolve telemetry (the model's answer failed; samples so far are kept)"
    : firstOpened !== null ? `sindri evolve show ${firstOpened}` : "sindri evolve proposals";
  return success([...lines, `Next: ${next}`].join("\n"), { since: day, hooks: rows, opened: firstOpened, stopped, dropped }, json, stopped !== null ? 1 : 0);
}
