import { parseFlags } from "../../args.js";
import { success, type CommandResult } from "../../output.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import { isHoldout, readCorpus } from "../corpus.js";
import { findMerged, setStatus, STATUSES, type ProposalStatus } from "../proposals.js";

export interface Section {
  lines: string[];
  data: Record<string, unknown>;
  attention: boolean;
  next: string | null;
}
export type SectionFn = (ctx: EvolveCtx) => Promise<Section>;

type State = "ok" | "FAIL" | "stale" | "untested" | "no-suite";

interface Row {
  id: string;
  kind: string;
  protected: number;
  hash: string;
  suite: string | null;
  run_ok: number | null;
  run_hash: string | null;
  open: number;
}

export function stateOf(r: Pick<Row, "suite" | "run_ok" | "run_hash" | "hash">): State {
  if (r.suite === null) return "no-suite";
  if (r.run_ok === null) return "untested";
  if (r.run_hash !== r.hash) return "stale";
  return r.run_ok === 1 ? "ok" : "FAIL";
}

// "at:<sha>" rows come from `check --at` (a channel build), so they never make a working-tree artifact look stale.
const ROWS = `
  SELECT a.id, a.kind, a.protected, a.hash, a.suite,
    (SELECT ok FROM suite_runs s WHERE s.artifact_id = a.id AND s.hash NOT LIKE 'at:%' ORDER BY s.seq DESC LIMIT 1) AS run_ok,
    (SELECT hash FROM suite_runs s WHERE s.artifact_id = a.id AND s.hash NOT LIKE 'at:%' ORDER BY s.seq DESC LIMIT 1) AS run_hash,
    (SELECT COUNT(*) FROM proposals p WHERE p.artifact_id = a.id AND p.status NOT IN ('rejected', 'adopted', 'lost', 'merged')) AS open
  FROM artifacts a WHERE a.removed_at IS NULL ORDER BY a.id`;

export async function artifactSection(ctx: EvolveCtx): Promise<Section> {
  const rows = ctx.db.prepare(ROWS).all() as Row[];
  if (rows.length === 0) return { lines: ["No artifacts registered yet."], data: { artifacts: [] }, attention: false, next: "sindri evolve init" };
  const items = rows.map((r) => ({ id: r.id, kind: r.kind, state: stateOf(r), protected: r.protected === 1, openProposals: r.open }));
  const lines = items.map((i) => `${i.state.padEnd(8)} ${i.id}${i.protected ? "  protected" : ""}${i.openProposals > 0 ? `  ${i.openProposals} open proposal(s)` : ""}`);
  const failing = items.filter((i) => i.state === "FAIL").map((i) => i.id);
  const needsRun = items.some((i) => i.state === "stale" || i.state === "untested");
  let next: string | null = null;
  if (failing.length > 0) next = `sindri evolve check ${failing.join(" ")}   (after fixing)`;
  else if (needsRun) next = "sindri evolve check --changed";
  return { lines, data: { artifacts: items }, attention: failing.length > 0 || items.some((i) => i.state === "stale"), next };
}

export async function proposalSection(ctx: EvolveCtx): Promise<Section> {
  const ids = await findMerged(ctx.db, ctx.deps.git, ctx.repo, repoConfig(ctx.loaded).defaultBranch);
  if (ids.length > 0) await ctx.writeRetry((epoch) => ids.forEach((id) => setStatus(ctx.db, id, "merged", epoch, ctx.deps.now())));
  const counts = Object.fromEntries(STATUSES.map((s) => [s, 0])) as Record<ProposalStatus, number>;
  for (const r of ctx.db.prepare("SELECT status, COUNT(*) AS c FROM proposals GROUP BY status").all() as { status: ProposalStatus; c: number }[]) counts[r.status] = r.c;
  const cap = ctx.loaded.profile.evolve.maxOpenProposals;
  const present = STATUSES.filter((s) => counts[s] > 0);
  const inFlight = counts.staged + counts.published;
  const shipped = counts.merged + counts.published;
  const mergeRate = shipped === 0 ? null : counts.merged / shipped;
  const lines =
    present.length === 0
      ? []
      : [
          `Proposals: ${present.map((s) => `${counts[s]} ${s}`).join(", ")}`,
          `In flight: ${inFlight} of ${cap} (evolve.maxOpenProposals)`,
          ...(mergeRate === null ? [] : [`Merge rate: ${counts.merged} of ${shipped} published proposals merged (${Math.round(mergeRate * 100)}%)`]),
          ...(counts.held === 0 ? [] : [`Held: ${counts.held} proposal(s) withheld by publish's privacy gate; they don't count against the cap. Reject one with: sindri evolve reject <id> --reason "..."`]),
        ];
  return {
    lines,
    data: { proposals: Object.fromEntries(present.map((s) => [s, counts[s]])), inFlight, cap, mergeRate },
    attention: false,
    next: counts.held > 0 ? "sindri evolve proposals --status held" : counts.proposed > 0 ? "sindri evolve proposals --status proposed" : null,
  };
}

const MIN_HOLDOUT = 20;

export async function corpusSection(ctx: EvolveCtx): Promise<Section> {
  const { items, dropped, unverified } = readCorpus(ctx.deps, "scope.draft");
  if (items.length === 0 && dropped.length === 0) return { lines: [], data: {}, attention: false, next: null };
  const holdout = items.filter((i) => isHoldout(i.id)).length;
  const needed = Math.max(0, MIN_HOLDOUT - holdout);
  const first = `Corpus: ${items.length} items (${holdout} holdout); ${needed > 0 ? `${needed} more holdout items needed, about ${Math.ceil((needed * 10) / 3)} more scope runs (30% of runs join the holdout).` : "enough for a comparison."}`;
  const lines = dropped.length > 0 ? [first, `Corpus check: ${dropped.length} item(s) failed the manifest and ledger check and are ignored${unverified === null ? "" : ` (${unverified})`}.`] : [first];
  return { lines, data: { corpus: { items: items.length, holdout, needed, dropped: dropped.length } }, attention: dropped.length > 0, next: null };
}

export const SECTIONS: SectionFn[] = [artifactSection, proposalSection, corpusSection];

export async function status(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const sections: Section[] = [];
  for (const fn of SECTIONS) sections.push(await fn(ctx));
  const next = sections.map((s) => s.next).find((n) => n !== null) ?? "sindri evolve proposals";
  const text = [...sections.flatMap((s) => s.lines), `Next: ${next}`].join("\n");
  const data = sections.reduce<Record<string, unknown>>((acc, s) => ({ ...acc, ...s.data }), {});
  return success(text, data, values.json === true, sections.some((s) => s.attention) ? 1 : 0);
}
