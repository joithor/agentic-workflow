import { parseFlags } from "../../args.js";
import { success, type CommandResult } from "../../output.js";
import type { EvolveCtx } from "../ctx.js";
import { terminalSafe } from "../invisible.js";
import { publishProposals, stageProposals } from "../stage.js";

export async function stage(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const o = await stageProposals(ctx);
  const dupes = o.duplicates > 0 ? [`${o.duplicates} duplicate(s) of merged or adopted work rejected`] : [];
  const extras = [...(o.selfAdopt > 0 ? [`${o.selfAdopt} prompt variant(s) wait for compare`] : []), ...dupes];
  let text: string;
  if (o.staged.length > 0) {
    const approval = o.staged.filter((s) => s.tier === "approval").length;
    const parts = [
      `Staged ${o.staged.length} proposal(s) (${approval} approval tier) in ${o.dir}`,
      ...(o.waiting > 0 ? [`${o.waiting} more waiting (cap ${o.cap}, ${o.inFlight + o.staged.length} in flight)`] : []),
      ...extras,
    ];
    text = `${parts.join("; ")}.\nNext: sindri evolve publish`;
  } else if (o.waiting > 0) {
    text = `Cap reached: ${o.inFlight} proposals are staged or published and not merged yet (evolve.maxOpenProposals is ${o.cap}); ${[`${o.waiting} waiting`, ...dupes].join("; ")}.\nNext: merge or reject some (sindri evolve proposals), then rerun sindri evolve stage`;
  } else {
    text = `Nothing to stage${extras.map((e) => `; ${e}`).join("")}.\nNext: sindri evolve proposals`;
  }
  return success(text, o, values.json === true);
}

export async function publish(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { "dry-run": { type: "boolean" }, "no-privacy-terms": { type: "boolean" }, json: { type: "boolean" } });
  const r = await publishProposals(ctx, { dryRun: values["dry-run"] === true, noPrivacyTerms: values["no-privacy-terms"] === true });
  const json = values.json === true;
  const heldLines = r.held.map((h) => `held ${h.id}: ${h.why}`);
  const heldHelp = r.held.length > 0 ? ['Held proposals stay held and don\'t count against the cap. Reject one with: sindri evolve reject <id> --reason "..."'] : [];
  const warning = [
    ...(r.termsSkipped ? ["Warning: no privacy.denyTerms were set; only the email-address and home-path checks ran."] : []),
    ...(r.warning === null ? [] : [`Warning: ${r.warning}`]),
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
