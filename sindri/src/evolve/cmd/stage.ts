import { parseFlags } from "../../args.js";
import { success, type CommandResult } from "../../output.js";
import type { EvolveCtx } from "../ctx.js";
import { terminalSafe } from "../invisible.js";
import { HOLD_LAYERS } from "../privacy.js";
import { publishProposals, stageProposals, type StageOutcome } from "../stage.js";

// The stage report: a summary line (the weekly digest uses it as it is), then one line per held proposal, then Next.
export function stageReport(o: StageOutcome): { summary: string; lines: string[] } {
  const dupes = o.duplicates > 0 ? [`${o.duplicates} duplicate(s) of merged or adopted work rejected`] : [];
  const extras = [...(o.selfAdopt > 0 ? [`${o.selfAdopt} prompt variant(s) wait for compare`] : []), ...dupes];
  const heldNote = o.held.length > 0 ? [`${o.held.length} held by the generalization pass`] : [];
  const heldLines = o.held.map((h) => `held ${h.id}: ${h.why}`);
  let summary: string;
  let next: string;
  if (o.staged.length > 0) {
    const approval = o.staged.filter((s) => s.tier === "approval").length;
    const parts = [
      `Staged ${o.staged.length} proposal(s) (${approval} approval tier) in ${o.dir}`,
      ...(o.waiting > 0 ? [`${o.waiting} more waiting (cap ${o.cap}, ${o.inFlight + o.staged.length} in flight)`] : []),
      ...heldNote,
      ...extras,
    ];
    summary = `${parts.join("; ")}.`;
    next = "Next: sindri evolve publish";
  } else if (o.waiting > 0 && o.inFlight >= o.cap) {
    summary = `Cap reached: ${o.inFlight} proposals are staged or published and not merged yet (evolve.maxOpenProposals is ${o.cap}); ${[`${o.waiting} waiting`, ...heldNote, ...dupes].join("; ")}.`;
    next = "Next: merge or reject some (sindri evolve proposals), then rerun sindri evolve stage";
  } else if (o.waiting > 0) {
    // Below the cap but nothing staged: held proposals took this run's slots, or the token budget ran out first.
    summary = `Nothing staged; ${[...heldNote, `${o.waiting} waiting for the next run`, ...extras].join("; ")}.`;
    next = "Next: rerun sindri evolve stage";
  } else {
    summary = `Nothing to stage${[...heldNote, ...extras].map((e) => `; ${e}`).join("")}.`;
    next = "Next: sindri evolve proposals";
  }
  return { summary, lines: [summary, ...heldLines, next] };
}

export async function stage(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const o = await stageProposals(ctx);
  return success(terminalSafe(stageReport(o).lines.join("\n")), o, values.json === true);
}

export async function publish(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { "dry-run": { type: "boolean" }, "no-privacy-terms": { type: "boolean" }, json: { type: "boolean" } });
  const r = await publishProposals(ctx, { dryRun: values["dry-run"] === true, noPrivacyTerms: values["no-privacy-terms"] === true });
  const json = values.json === true;
  // Each line names the layer and its check, never the matched text; then one count per layer, in gate order.
  const byLayer = HOLD_LAYERS.flatMap((l) => (r.heldByLayer[l] === undefined ? [] : [`${l} ${r.heldByLayer[l]}`]));
  const heldLines = [...r.held.map((h) => `held ${h.id} (${h.layer}): ${h.why}`), ...(byLayer.length > 0 ? [`Held by layer: ${byLayer.join(", ")}`] : [])];
  const heldHelp = r.held.length > 0 ? ['Held proposals stay held and don\'t count against the cap. Reject one with: sindri evolve reject <id> --reason "..."'] : [];
  const warning = [
    ...(r.termsSkipped ? ["Warning: no privacy.denyTerms were set, so the private-terms layer was skipped; the patterns, identifiers, verbatim, personal and scrubber layers ran."] : []),
    ...(r.warning === null ? [] : [`Warning: ${r.warning}`]),
    // The one fail-open default of the gate: an unset private: skips the identifiers layer for that repo. Names stay out.
    ...(r.privateUnset > 0 ? [`Warning: ${r.privateUnset} repo(s) other than the toolkit repo don't set private:, so publish treats them as public (sindri doctor lists them).`] : []),
  ];
  const heldNote = r.held.length > 0 ? `; ${r.held.length} held back` : "";
  if (r.published.length === 0 && r.held.length === 0) return success("Nothing is staged.\nNext: sindri evolve stage", { ...r }, json);
  if (r.published.length === 0) return success(terminalSafe([`Nothing to publish: ${r.held.length} held back.`, ...heldLines, ...heldHelp].join("\n")), { ...r }, json, 1);
  const range = `tasks ${r.first}-${r.first + r.published.length - 1}`;
  const commands = ["Next: review the diff, then commit it:", `  git add ${r.relFile}`, `  git commit -m "docs: sindri proposals, week of ${r.monday}"`];
  const lines = r.dryRun
    ? [`Would publish ${r.published.length} proposal(s) as ${range} in ${r.relFile} (dry run; nothing was written)${heldNote}.`, ...heldLines, ...warning]
    : [`Published ${r.published.length} proposal(s) as ${range} in ${r.relFile}${heldNote}.`, ...heldLines, ...warning, ...commands, ...heldHelp];
  return success(terminalSafe(lines.join("\n")), { ...r }, json, r.held.length > 0 ? 1 : 0);
}
