import fs from "node:fs";

import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { audit } from "../audit.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import {
  classifyTier, getProposal, latestComparison, listProposals, nextFor, owns, reduceEvidence, setStatus, stagedFile, STATUSES, TERMINAL, type ProposalStatus,
} from "../proposals.js";
import { isAddedTestAllowed, isProtectedPath, loadRegistry } from "../registry.js";

const ageDays = (iso: string, now: Date): number => Math.max(0, Math.floor((now.getTime() - Date.parse(iso)) / 86_400_000));

export async function proposals(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { status: { type: "string" }, all: { type: "boolean" }, json: { type: "boolean" } });
  const wanted = values.status === undefined ? null : values.status.split(",");
  const unknown = wanted?.find((s) => !STATUSES.includes(s as ProposalStatus));
  if (unknown !== undefined) throw new SindriError("SND-CLI-002", `unknown status: ${unknown}; use ${STATUSES.join(", ")}`);
  const statuses = wanted === null ? (values.all === true ? undefined : STATUSES.filter((s) => !TERMINAL.includes(s))) : (wanted as ProposalStatus[]);
  const rows = listProposals(ctx.db, statuses);
  const now = ctx.deps.now();
  let text: string;
  if (rows.length === 0) {
    text = wanted === null ? "No proposals yet.\nNext: sindri evolve reflect --pr <n>" : `No proposals with status ${wanted.join(" or ")}.\nNext: sindri evolve proposals --all`;
  } else {
    text = [...rows.map((r) => `${r.status.padEnd(19)} ${r.tier.padEnd(10)} ${r.id}  ${r.artifact}: ${r.title}  (${ageDays(r.createdAt, now)}d)`), `Next: sindri evolve show ${rows[0].id}`].join("\n");
  }
  return success(text, { proposals: rows.map((r) => ({ ...r, ageDays: ageDays(r.createdAt, now) })) }, values.json === true);
}

export async function show(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const id = positionals[0];
  if (id === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve show <id>");
  const s = getProposal(ctx.db, id);
  if (s === null) throw new SindriError("SND-EVOLVE-008", `no such proposal: ${id}`);
  const p = s.proposal;
  const now = classifyTier(p, loadRegistry(ctx.db), repoConfig(ctx.loaded).protectedPaths);
  const tierText = now.tier === s.tier ? `${s.tier} (${now.why})` : `${s.tier} (recomputed: ${now.tier}, ${now.why})`;
  const ev = reduceEvidence(p.evidence);
  const cmp = latestComparison(ctx.db, id);
  const indent = (t: string): string => t.split("\n").map((l) => `  ${l}`).join("\n");
  const lines = [
    `Proposal ${id}: ${p.title}`,
    `Status: ${s.status}   Tier: ${tierText}   Source: ${s.source}`,
    `Artifact: ${p.artifact}   Kind: ${p.kind}`,
    p.change.type === "describe" ? `Files: ${p.change.files.join(", ")}` : `Prompt text: ${p.change.text.length} characters`,
    `Why:\n${indent(p.rationale)}`,
    ...(p.change.type === "describe" ? [`Change:\n${indent(p.change.description)}`] : []),
    `Evidence: ${ev.refs.length > 0 ? ev.refs.join(", ") : "none recorded"}${ev.withheld > 0 ? ` (${ev.withheld} reference(s) withheld)` : ""}`,
    ...(cmp === null ? [] : [`Comparison (run ${cmp.run}): ${cmp.line}`]),
    `Next: ${nextFor(s)}`,
  ];
  return success(lines.join("\n"), { ...s, recomputed: now, evidence: ev.refs, comparison: cmp }, values.json === true);
}

export async function reject(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { reason: { type: "string" }, json: { type: "boolean" } });
  const id = positionals[0];
  const reason = values.reason?.trim() ?? "";
  if (id === undefined || reason === "") throw new SindriError("SND-CLI-002", 'usage: sindri evolve reject <id> --reason "why"');
  const s = getProposal(ctx.db, id);
  if (s === null) throw new SindriError("SND-EVOLVE-008", `no such proposal: ${id}`);
  if (TERMINAL.includes(s.status)) return success(`Proposal ${id} is already ${s.status}; nothing to do.\nNext: sindri evolve proposals`, { id, status: s.status }, values.json === true);
  ctx.write((epoch) => {
    setStatus(ctx.db, id, "rejected", epoch, ctx.deps.now());
    audit(ctx.db, ctx.deps, "reject", `${id}: ${reason}`, epoch);
  });
  fs.rmSync(stagedFile(ctx.deps, id), { force: true });
  return success(`Rejected ${id}. The same proposal won't be saved again.\nNext: sindri evolve proposals`, { id, status: "rejected" }, values.json === true);
}

// The merge-gate check for H4: recompute the tier from the files the PR really changed.
export async function tier(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { base: { type: "string" }, json: { type: "boolean" } });
  const id = positionals[0];
  if (id === undefined) throw new SindriError("SND-CLI-002", "usage: sindri evolve tier <id> [--base <ref>]");
  const s = getProposal(ctx.db, id);
  if (s === null) throw new SindriError("SND-EVOLVE-008", `no such proposal: ${id}`);
  const cfg = repoConfig(ctx.loaded);
  const base = values.base ?? cfg.defaultBranch;
  // --no-renames: a rename shows as a delete plus an add, so a moved protected file is still seen.
  const names = (extra: string[]) => ctx.deps.git.run(["diff", "--name-only", "--no-renames", ...extra, `${base}...HEAD`], ctx.repo);
  const [diff, addedDiff, tree] = [await names([]), await names(["--diff-filter=A"]), await ctx.deps.git.run(["ls-tree", "-r", "--name-only", base], ctx.repo)];
  if (!diff.ok || !addedDiff.ok || !tree.ok) throw new SindriError("SND-EVOLVE-001", `couldn't diff against ${base}`);
  const lines = (out: string): string[] => out.split("\n").filter((f) => f !== "");
  const files = lines(diff.stdout);
  const added = new Set(lines(addedDiff.stdout));
  const tracked = lines(tree.stdout);
  const hit = files.filter((f) => isProtectedPath(f, cfg.protectedPaths) && !(added.has(f) && isAddedTestAllowed(f, tracked, cfg.protectedPaths)));
  // The same ownership rule classifyTier applies: a change outside the proposal's artifact is approval. An unknown artifact owns nothing.
  const artifact = loadRegistry(ctx.db).find((a) => a.id === s.artifact);
  const outside = files.filter((f) => !hit.includes(f) && (artifact === undefined || !owns(artifact, f)));
  const actual = hit.length > 0 || outside.length > 0 || s.tier === "approval" ? "approval" : s.tier;
  const protectedText = [
    hit.length > 0 ? `${hit.length} protected: ${hit.slice(0, 5).join(", ")}` : "0 protected",
    ...(outside.length > 0 ? [`${outside.length} outside ${s.artifact}: ${outside.slice(0, 5).join(", ")}`] : []),
  ].join(", ");
  const stricter = actual === "approval" && s.tier !== "approval";
  const text = `Proposal ${id} declared ${s.tier}; the diff against ${base} touches ${files.length} file(s), ${protectedText}. Actual tier: ${actual}.\nNext: ${actual === "approval" ? "get the owner's approval before merging" : "open the PR"}`;
  return success(text, { id, declared: s.tier, actual, files, protectedFiles: hit, outsideFiles: outside }, values.json === true, stricter ? 1 : 0);
}
