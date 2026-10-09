import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { profileScrubber } from "../../scope/commands.js";
import { Budget } from "../../scope/model.js";
import { compareScopeDraft, MIN_DECIDED, MIN_ITEMS, tooManyErrors, type CompareResult, type CompareStatus } from "../compare.js";
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
  const tail = r.errors > 0 ? ` ${r.errors} item(s) errored (a failure on the variant alone counts as a loss, any other as a tie).` : "";
  switch (r.status) {
    case "won":
    case "lost":
      return `${r.status}: ${r.wins} of ${r.wins + r.losses} decided pairs (${rate}) on ${r.n} holdout items; ${r.ties} ties.${tail}`;
    case "inconclusive":
      return `inconclusive: ${[
        ...(tooManyErrors(r.errors, r.n) ? [`${r.errors} of ${r.n} items errored, more than 10%`] : []),
        ...(r.wins + r.losses < MIN_DECIDED ? [`only ${r.wins + r.losses} of ${r.n} pairs were decided (need ${MIN_DECIDED})`] : []),
      ].join("; ")}; ${r.ties} ties.${tail}`;
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

// A closing write always records its row, but moves the proposal's status only when this run is the highest
// run and nothing else (reject, defer, a newer run) has changed the status since it was set to `evaluating`.
function closeStatus(ctx: EvolveCtx, id: string, run: number, to: ProposalStatus, epoch: number): void {
  const newest = (ctx.db.prepare("SELECT MAX(run) AS r FROM comparisons WHERE proposal_id = ?").get(id) as { r: number }).r;
  const now = (ctx.db.prepare("SELECT status FROM proposals WHERE id = ?").get(id) as { status: ProposalStatus }).status;
  if (run === newest && now === "evaluating") setStatus(ctx.db, id, to, epoch, ctx.deps.now());
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
  const budgetLimit = ctx.loaded.profile.evolve.maxTokensPerCompare;
  const corpus = readCorpus(ctx.deps, "scope.draft").items;
  const total = split(corpus).holdout.length;
  // No tick lock is held while the models run: each ledger write takes it only for its own batch. The status
  // check, the compared-once check, the run number and the `running` marker all happen in ONE write, so two
  // overlapping comparisons can't both pass the check or share a run number. "Latest" is the highest run number.
  const start = await ctx.writeRetry((epoch) => {
    const now = (ctx.db.prepare("SELECT status FROM proposals WHERE id = ?").get(id) as { status: ProposalStatus }).status;
    if (!COMPARABLE.includes(now)) throw new SindriError("SND-EVOLVE-016", `proposal ${id} is ${now}; it is not compared again`);
    const prev = ctx.db.prepare("SELECT run, verdict, detail FROM comparisons WHERE proposal_id = ? AND item_id = '*' ORDER BY run DESC, seq DESC LIMIT 1").get(id) as
      | { run: number; verdict: string; detail: string }
      | undefined;
    if (values.rerun !== true && prev !== undefined) {
      if (prev.verdict === "running") throw new SindriError("SND-EVOLVE-016", `a comparison of ${id} is already running (run ${prev.run}); pass --rerun to start another`);
      if (VERDICTS.includes(prev.verdict as CompareStatus)) return { kind: "stored" as const, run: prev.run, verdict: prev.verdict as CompareStatus, line: (JSON.parse(prev.detail) as { line: string }).line };
    }
    const next = (ctx.db.prepare("SELECT COALESCE(MAX(run), 0) + 1 AS n FROM comparisons WHERE proposal_id = ?").get(id) as { n: number }).n;
    // A rerun supersedes every earlier run still open (a killed compare leaves its `running` marker behind, and
    // an open marker blocks adopt forever). Close each one here, in the same write that takes the new number.
    const open = values.rerun !== true ? [] : ctx.db.prepare(
      "SELECT run FROM comparisons m WHERE proposal_id = ? AND item_id = '*' AND verdict = 'running' AND NOT EXISTS (SELECT 1 FROM comparisons c WHERE c.proposal_id = m.proposal_id AND c.run = m.run AND c.item_id = '*' AND c.verdict != 'running') ORDER BY run",
    ).all(id) as { run: number }[];
    for (const o of open) {
      ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', 'errored', ?, ?, ?)")
        .run(id, o.run, JSON.stringify({ line: `superseded by run ${next}` }), ctx.deps.now().toISOString(), epoch);
    }
    ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', 'running', '{}', ?, ?)").run(id, next, ctx.deps.now().toISOString(), epoch);
    setStatus(ctx.db, id, "evaluating", epoch, ctx.deps.now());
    return { kind: "go" as const, run: next, before: now };
  });
  if (start.kind === "stored") {
    const text = [`Stored result (run ${start.run}): ${start.line}`, "A proposal is compared once; pass --rerun to compare again (the rerun is recorded).", `Next: ${nextFor(start.verdict, id)}`].join("\n");
    return success(text, { id, status: start.verdict, run: start.run, line: start.line, stored: true }, json);
  }
  const { run, before } = start;
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
    // Close the run with an `errored` row so a crash never leaves a dangling `running` marker.
    const message = profileScrubber(ctx.loaded).scrub(e instanceof Error ? e.message : String(e)).text.slice(0, 500);
    // Best-effort: if the closing write fails too, the original error is the one worth reporting.
    try {
      await ctx.writeRetry((epoch) => {
        ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', 'errored', ?, ?, ?)")
          .run(id, run, JSON.stringify({ line: `errored: ${message}` }), ctx.deps.now().toISOString(), epoch);
        closeStatus(ctx, id, run, before === "evaluating" ? "proposed" : before, epoch);
      });
    } catch {
      // swallowed on purpose: see above
    }
    throw e;
  }
  const line = lineFor(result, budgetLimit);
  const scrubber = profileScrubber(ctx.loaded);
  await ctx.writeRetry((epoch) => {
    const ts = ctx.deps.now().toISOString();
    for (const p of result.perItem) {
      ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, ?, ?, ?, ?, ?)").run(id, run, p.id, p.verdict, JSON.stringify({ reason: p.reason === undefined ? null : scrubber.scrub(p.reason).text }), ts, epoch);
    }
    const summary = { status: result.status, n: result.n, wins: result.wins, losses: result.losses, ties: result.ties, errors: result.errors, winRate: result.winRate, lower: result.lower, leaks: result.leaks, line };
    ctx.db.prepare("INSERT INTO comparisons (proposal_id, run, item_id, verdict, detail, ts, epoch) VALUES (?, ?, '*', ?, ?, ?, ?)").run(id, run, result.status, JSON.stringify(summary), ts, epoch);
    closeStatus(ctx, id, run, proposalStatusFor(result.status, before), epoch);
  });
  const decisive = result.status === "won" || result.status === "lost";
  return success(`${line}\nNext: ${nextFor(result.status, id)}`, { id, run, ...result, line, stored: false }, json, decisive ? 0 : 1);
}
