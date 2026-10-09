import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { lineDiff } from "../../profile/approve.js";
import { removeOverlay, writeOverlay } from "../adopt.js";
import { audit } from "../audit.js";
import { lintLeaks } from "../blind.js";
import type { EvolveCtx } from "../ctx.js";
import { loadPrompt, sha256 } from "../overlay.js";
import { hasSafetyClause, PROMPT_IDS } from "../prompts.js";
import { comparisonRuns, getProposal, latestComparison, runningComparison, setStatus } from "../proposals.js";

const refuse = (why: string): SindriError => new SindriError("SND-EVOLVE-004", why);

export async function adopt(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const id = positionals[0];
  if (id === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve adopt <id>");
  const s = getProposal(ctx.db, id);
  if (s === null) throw new SindriError("SND-EVOLVE-008", `no such proposal: ${id}`);
  const change = s.proposal.change;
  if (change.type !== "replace-prompt" || s.status !== "won") throw refuse(`only a won prompt variant can be adopted (this one is ${s.status})`);
  const promptId = PROMPT_IDS.find((p) => `prompt:${p}` === s.artifact);
  if (promptId === undefined) throw refuse(`${s.artifact} isn't a prompt artifact`);
  const running = runningComparison(ctx.db, id);
  if (running !== null) throw refuse(`a comparison is still running (run ${running}); wait for it to finish`);
  // The latest run decides: a won run followed by an errored, lost or inconclusive rerun doesn't count.
  const cmp = latestComparison(ctx.db, id);
  if (cmp === null) throw refuse("no won comparison is on record for this proposal");
  if (cmp.status !== "won") throw refuse(`no won comparison is on record for this proposal (the latest, run ${cmp.run}, is ${cmp.status})`);
  if (!hasSafetyClause(promptId, change.text) || lintLeaks(change.text).length > 0) throw refuse("the variant must keep the safety clause and pass the leak check");
  if (!ctx.deps.isTTY) throw new SindriError("SND-EVOLVE-006", "adopting a prompt needs an interactive terminal");
  const sha = sha256(change.text);
  const diff = lineDiff(loadPrompt(ctx.deps, promptId).split("\n"), change.text.split("\n"));
  const answer = await ctx.deps.prompt([
    `Adopt this variant for ${promptId}?`, ...diff, "", `Comparison: ${cmp.line}`,
    `Comparison runs so far: ${comparisonRuns(ctx.db, id)} (a variant that kept rerunning until it won is not evidence; check the history in sindri evolve show)`,
    `Type the first 8 characters of the variant's sha256 (${sha.slice(0, 8)}) to confirm: `,
  ].join("\n"));
  if (answer.trim() !== sha.slice(0, 8)) throw new SindriError("SND-EVOLVE-007", "the confirmation didn't match");
  const written = ctx.write((epoch) => {
    const w = writeOverlay(ctx.deps, promptId, change.text);
    ctx.db.prepare("INSERT INTO adoptions (prompt_id, proposal_id, sha256, adopted_at, adopted_by, epoch) VALUES (?, ?, ?, ?, ?, ?)")
      .run(promptId, id, w.sha, ctx.deps.now().toISOString(), ctx.deps.system.username(), epoch);
    setStatus(ctx.db, id, "adopted", epoch, ctx.deps.now());
    audit(ctx.db, ctx.deps, "adopt", `${id} -> ${promptId} sha256 ${w.sha.slice(0, 16)}`, epoch);
    return w;
  });
  return success(`Adopted ${id}: wrote ${written.file} (sha256 ${sha.slice(0, 8)}). It takes effect on the next sindri scope run.\nNext: sindri evolve status`, { id, prompt: promptId, file: written.file, sha256: written.sha }, values.json === true);
}

export async function revert(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const promptId = PROMPT_IDS.find((p) => p === positionals[0]);
  if (promptId === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve revert <prompt-id> (one of " + PROMPT_IDS.join(", ") + ")");
  if (!ctx.deps.isTTY) throw new SindriError("SND-EVOLVE-006", "reverting a prompt needs an interactive terminal");
  const had = ctx.write((epoch) => {
    const present = removeOverlay(ctx.deps, promptId);
    if (present) {
      // A "reverted" row becomes the latest adoption, so a file an attacker puts back never matches.
      ctx.db.prepare("INSERT INTO adoptions (prompt_id, proposal_id, sha256, adopted_at, adopted_by, epoch) VALUES (?, 'revert', 'reverted', ?, ?, ?)")
        .run(promptId, ctx.deps.now().toISOString(), ctx.deps.system.username(), epoch);
      audit(ctx.db, ctx.deps, "revert", promptId, epoch);
    }
    return present;
  });
  const text = had ? `Reverted ${promptId} to the built-in prompt.` : `There is no overlay for ${promptId}; nothing to revert.`;
  return success(`${text}\nNext: sindri evolve status`, { prompt: promptId, reverted: had }, values.json === true);
}
