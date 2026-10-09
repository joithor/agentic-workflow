import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { profileScrubber } from "../../scope/commands.js";
import { Budget } from "../../scope/model.js";
import { compareScopeDraft, MIN_DECIDED, MIN_ITEMS, type CompareResult, type CompareStatus } from "../compare.js";
import { readCorpus, split } from "../corpus.js";
import type { EvolveCtx } from "../ctx.js";
import { loadPrompt } from "../overlay.js";
import { SOURCES_CLAUSE } from "../prompts.js";
import { getProposal, setStatus, type ProposalStatus } from "../proposals.js";

// Results that are verdicts: a proposal is compared once unless --rerun. The rest can be rerun freely.
const VERDICTS: readonly CompareStatus[] = ["won", "lost", "inconclusive"];

// A proposal in any other status (rejected, adopted, merged, staged, published, held) is done with
// comparing; a comparison must not resurrect it.
const COMPARABLE: readonly ProposalStatus[] = ["proposed", "evaluating", "won", "lost", "insufficient-corpus"];

const proposalStatusFor = (s: CompareStatus, before: ProposalStatus): ProposalStatus => {
  if (s === "won") return "won";
  if (s === "lost" || s === "leaky-variant" || s === "missing-safety-clause") return "lost";
  if (s === "insufficient-corpus") return "insufficient-corpus";
  // inconclusive and incomplete decide nothing: a lost proposal stays lost, anything else (a `won` from an
  // earlier run included, which this run no longer supports) goes back to proposed.
  return before === "lost" ? "lost" : "proposed";
};

function lineFor(r: CompareResult, budget: number): string {
  const rate = `win rate ${r.winRate.toFixed(2)}, lower bound ${r.lower.toFixed(2)}`;
  const tail = r.errors > 0 ? ` ${r.errors} item(s) errored and count as ties.` : "";
  switch (r.status) {
    case "won":
    case "lost":
      return `${r.status}: ${r.wins} of ${r.wins + r.losses} decided pairs (${rate}) on ${r.n} holdout items; ${r.ties} ties.${tail}`;
    case "inconclusive":
      return `inconclusive: only ${r.wins + r.losses} of ${r.n} pairs were decided (need ${MIN_DECIDED}); ${r.ties} ties.${tail}`;
    case "insufficient-corpus":
      return `insufficient-corpus: ${r.n} holdout items, need ${MIN_ITEMS}. About ${Math.ceil(((MIN_ITEMS - r.n) * 10) / 3)} more scope runs would add the missing ${MIN_ITEMS - r.n}.`;
    case "leaky-variant":
      return `leaky-variant: the variant mentions ${r.leaks.join(", ")}; remove it and propose again.`;
    case "missing-safety-clause":
      return `missing-safety-clause: the variant must keep this line: ${SOURCES_CLAUSE}`;
    case "incomplete":
      return `incomplete: the token budget (${budget}) ran out after ${r.n} item(s); nothing was decided.${tail}`;
  }
}

function nextFor(status: CompareStatus, id: string): string {
  switch (status) {
    case "won": return `sindri evolve adopt ${id}`;
    case "lost": return `sindri evolve reject ${id} --reason "lost the comparison"`;
    case "inconclusive": return `sindri evolve show ${id}`;
    case "insufficient-corpus": return "keep running sindri scope; sindri evolve status shows the corpus";
    case "leaky-variant": return `sindri evolve reject ${id} --reason "leaks the evaluation"`;
    case "missing-safety-clause": return `sindri evolve reject ${id} --reason "dropped the safety clause"`;
    case "incomplete": return "raise evolve.maxTokensPerCompare in the profile (then sindri profile approve), then rerun sindri evolve compare";
  }
}

export async function compare(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { rerun: { type: "boolean" }, json: { type: "boolean" } });
  const json = values.json === true;
  const id = positionals[0];
  if (id === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve compare <id> [--rerun]");
  const stored = getProposal(ctx.db, id);
  if (stored === null) throw new SindriError("SND-EVOLVE-008", `no such proposal: ${id}`);
  if (stored.proposal.change.type !== "replace-prompt" || stored.artifact !== "prompt:scope.draft") {
    throw new SindriError("SND-EVOLVE-002", `no offline comparison for ${stored.artifact} yet (spec amendment 4)`);
  }
  if (!COMPARABLE.includes(stored.status)) throw new SindriError("SND-EVOLVE-016", `proposal ${id} is ${stored.status}; it is not compared again`);
  const prev = ctx.db.prepare("SELECT run, verdict, detail FROM comparisons WHERE proposal_id = ? AND item_id = '*' ORDER BY seq DESC LIMIT 1").get(id) as
    | { run: number; verdict: CompareStatus; detail: string }
    | undefined;
  if (prev !== undefined && VERDICTS.includes(prev.verdict) && values.rerun !== true) {
    const line = (JSON.parse(prev.detail) as { line: string }).line;
    const text = [`Stored result (run ${prev.run}): ${line}`, "A proposal is compared once; pass --rerun to compare again (the rerun is recorded).", `Next: ${nextFor(prev.verdict, id)}`].join("\n");
    return success(text, { id, status: prev.verdict, run: prev.run, line, stored: true }, json);
  }
  const run = prev === undefined ? 1 : prev.run + 1;
  const budgetLimit = ctx.loaded.profile.evolve.maxTokensPerCompare;
  const corpus = readCorpus(ctx.deps, "scope.draft").items;
  const total = split(corpus).holdout.length;
  // No tick lock is held while the models run: each ledger write below takes it only for its own batch.
  await ctx.writeRetry((epoch) => setStatus(ctx.db, id, "evaluating", epoch, ctx.deps.now()));
  let result: CompareResult;
  try {
    result = await compareScopeDraft({
      items: corpus, current: loadPrompt(ctx.deps, "scope.draft"), variant: stored.proposal.change.text, runner: ctx.io.runner(ctx.loaded, profileScrubber(ctx.loaded)),
      models: { scoping: ctx.loaded.profile.models.scoping, judge: ctx.loaded.profile.models.adjudicator },
      budget: new Budget(budgetLimit), maxPackChars: ctx.loaded.profile.scope.maxPackChars,
      onProgress: (done) => ctx.deps.log(`compared ${done} of ${total} holdout items`),
    });
  } catch (e) {
    // SND-EVOLVE-009 (judge = drafter) or anything unexpected: don't strand the proposal in `evaluating`.
    await ctx.writeRetry((epoch) => setStatus(ctx.db, id, stored.status === "evaluating" ? "proposed" : stored.status, epoch, ctx.deps.now()));
    throw e;
  }
  const line = lineFor(result, budgetLimit);
  await ctx.writeRetry((epoch) => {
    const ts = ctx.deps.now().toISOString();
    for (const p of result.perItem) {
      ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, ?, ?, ?, ?, ?)").run(id, run, p.id, p.verdict, JSON.stringify({ reason: p.reason ?? null }), ts, epoch);
    }
    const summary = { status: result.status, n: result.n, wins: result.wins, losses: result.losses, ties: result.ties, errors: result.errors, winRate: result.winRate, lower: result.lower, leaks: result.leaks, line };
    ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', ?, ?, ?, ?)").run(id, run, result.status, JSON.stringify(summary), ts, epoch);
    setStatus(ctx.db, id, proposalStatusFor(result.status, stored.status), epoch, ctx.deps.now());
  });
  const decisive = result.status === "won" || result.status === "lost";
  return success(`${line}\nNext: ${nextFor(result.status, id)}`, { id, run, ...result, line, stored: false }, json, decisive ? 0 : 1);
}
