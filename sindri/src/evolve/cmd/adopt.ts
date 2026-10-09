import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { lineDiff } from "../../profile/approve.js";
import { removeOverlay, writeOverlay } from "../adopt.js";
import { audit } from "../audit.js";
import { lintVariant } from "../blind.js";
import { holdoutTitles } from "../corpus.js";
import type { EvolveCtx } from "../ctx.js";
import { escapeInvisible } from "../invisible.js";
import { loadPrompt, sha256 } from "../overlay.js";
import { defaultPrompt, hasSafetyClause, PROMPT_IDS, type PromptId } from "../prompts.js";
import { comparisonRuns, getProposal, latestComparison, runningComparison, setStatus } from "../proposals.js";
import { profileScrubber } from "../../scope/commands.js";

const refuse = (why: string): SindriError => new SindriError("SND-EVOLVE-004", why);

const TAB: ReadonlySet<string> = new Set(["\t"]);

interface Gate { promptId: PromptId; text: string; cmp: { run: number; line: string } }

// Every refusal that depends on the ledger. It runs before the prompt and again inside the write that records the adoption.
function gate(ctx: EvolveCtx, id: string): Gate {
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
  // The same lint compare ran, plus the holdout titles that joined the corpus since.
  if (!hasSafetyClause(promptId, change.text) || lintVariant(change.text, defaultPrompt(promptId), holdoutTitles(ctx.deps)).length > 0) {
    throw refuse("the variant must keep the safety clause and pass the leak check");
  }
  return { promptId, text: change.text, cmp };
}

export async function adopt(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const id = positionals[0];
  if (id === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve adopt <id>");
  const { promptId, text, cmp } = gate(ctx, id);
  if (!ctx.deps.isTTY) throw new SindriError("SND-EVOLVE-006", "adopting a prompt needs an interactive terminal");
  const sha = sha256(text);
  const diff = lineDiff(loadPrompt(ctx.deps, promptId).split("\n"), text.split("\n")).map((l) => escapeInvisible(l, TAB));
  const answer = await ctx.deps.prompt([
    `Adopt this variant for ${promptId}?`, ...diff, "", `Comparison: ${escapeInvisible(cmp.line)}`,
    `Comparison runs so far: ${comparisonRuns(ctx.db, id)} (a variant that kept rerunning until it won is not evidence; check the history in sindri evolve show)`,
    `Type the first 8 characters of the variant's sha256 (${sha.slice(0, 8)}) to confirm: `,
  ].join("\n"));
  if (answer.trim() !== sha.slice(0, 8)) throw new SindriError("SND-EVOLVE-007", "the confirmation didn't match");
  const written = ctx.write((epoch) => {
    // The prompt can wait for minutes: re-check everything that was true when it was shown.
    const now = gate(ctx, id);
    if (now.cmp.run !== cmp.run || sha256(now.text) !== sha) {
      throw refuse(`the proposal changed while you were confirming (shown run ${cmp.run}, now run ${now.cmp.run}${sha256(now.text) === sha ? "" : "; the variant text differs"}); nothing was written`);
    }
    const w = writeOverlay(ctx.deps, promptId, text);
    ctx.db.prepare("INSERT INTO adoptions (prompt_id, proposal_id, sha256, adopted_at, adopted_by, epoch) VALUES (?, ?, ?, ?, ?, ?)")
      .run(promptId, id, w.sha, ctx.deps.now().toISOString(), ctx.deps.system.username(), epoch);
    setStatus(ctx.db, id, "adopted", epoch, ctx.deps.now());
    audit(ctx.db, ctx.deps, "adopt", `${id} -> ${promptId} sha256 ${w.sha.slice(0, 16)}`, epoch, profileScrubber(ctx.loaded));
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
    const latest = ctx.db.prepare("SELECT proposal_id, sha256 FROM adoptions WHERE prompt_id = ? ORDER BY seq DESC LIMIT 1").get(promptId) as { proposal_id: string; sha256: string } | undefined;
    const stale = latest !== undefined && latest.sha256 !== "reverted"; // an adoption is still the latest word, even if its file is gone
    if (present || stale) {
      // A "reverted" row becomes the latest adoption, so a file an attacker (or a backup restore) puts back never matches.
      ctx.db.prepare("INSERT INTO adoptions (prompt_id, proposal_id, sha256, adopted_at, adopted_by, epoch) VALUES (?, 'revert', 'reverted', ?, ?, ?)")
        .run(promptId, ctx.deps.now().toISOString(), ctx.deps.system.username(), epoch);
      // The proposal statuses have no "reverted", so the adopted proposal keeps "adopted"; the ledger says so.
      audit(ctx.db, ctx.deps, "revert", stale ? `${promptId} (adopted proposal ${latest.proposal_id} keeps status adopted: there is no reverted status)` : promptId, epoch, profileScrubber(ctx.loaded));
    }
    return { present, reverted: present || stale };
  });
  const text = !had.reverted
    ? `There is no overlay for ${promptId}; nothing to revert.`
    : `Reverted ${promptId} to the built-in prompt${had.present ? "" : " (the overlay file was already gone; the adoption is now recorded as reverted)"}.`;
  return success(`${text}\nNext: sindri evolve status`, { prompt: promptId, reverted: had.reverted }, values.json === true);
}
