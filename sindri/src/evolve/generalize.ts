import { z } from "zod";
import { zodToJsonSchema } from "zod-to-json-schema";

import type { Budget, ModelRunner } from "../scope/model.js";
import { askModel } from "./ask.js";
import { TRANSCRIPTS_CLAUSE } from "./prompts.js";
import { ProposalSchema, type Proposal } from "./proposals.js";

// Privacy spec §5.1: every proposal is restated without workplace detail, then a separate call judges the
// restatement. These prompts are deliberately not prompt artifacts: evolve must never tune its own gate.
export const GENERALIZE_ROLES = { rewrite: "generalize.rewrite", judge: "generalize.judge" } as const;
export const LEAK_CATEGORIES = ["none", "company-or-product", "people", "customers", "codebase", "tickets", "domain", "other"] as const;

export const REWRITE_SYSTEM = [
  "You rewrite one proposal for improving an agent toolkit so it can be published in a public repository.",
  "The proposal and the transcript lines behind its evidence came from private work. Restate the proposal as a change to how agents behave or to the toolkit itself.",
  "Remove every detail about the product, company, customers, people, codebase (names of files, functions, tables, routes, packages), tickets and business domain. Describe the agent behavior in general terms; never quote the transcripts.",
  "Keep the artifact, the kind and the evidence ids exactly as given. Keep the change type; you may reword its description and must keep its files inside the toolkit.",
  TRANSCRIPTS_CLAUSE,
].join("\n");

export const JUDGE_SYSTEM = [
  "You check a proposal that is about to be published in a public repository. You also get the private transcript lines it was built from.",
  "Answer whether a reader of the proposal alone could learn anything specific to the private work: the product, company, customers, people, code names, tickets or business domain. Generic statements about agent behavior or the toolkit are fine.",
  `Return leaks (true or false) and one category from: ${LEAK_CATEGORIES.join(", ")}. Use none only when leaks is false.`,
  TRANSCRIPTS_CLAUSE,
].join("\n");

const Verdict = z.object({ leaks: z.boolean(), category: z.enum(LEAK_CATEGORIES) });
const PROPOSAL_SCHEMA = zodToJsonSchema(ProposalSchema, { $refStrategy: "none" }) as Record<string, unknown>;
const VERDICT_SCHEMA = zodToJsonSchema(Verdict, { $refStrategy: "none" }) as Record<string, unknown>;
const EVIDENCE_CHARS = 2_000;
const EVIDENCE_TOTAL = 40_000;

export type Generalized = { ok: true; proposal: Proposal; category: string; model: string } | { ok: false; why: string };

const esc = (s: string): string => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

function evidenceBlock(evidence: ReadonlyMap<string, string>): string {
  const parts: string[] = [];
  let total = 0;
  for (const [ref, text] of evidence) {
    const piece = `<untrusted id="${esc(ref)}">${esc(text.slice(0, EVIDENCE_CHARS))}</untrusted>`;
    if (total + piece.length > EVIDENCE_TOTAL) break;
    parts.push(piece);
    total += piece.length;
  }
  return parts.length === 0 ? "(no transcript text could be resolved for this proposal's evidence)" : parts.join("\n");
}

const sameRefs = (a: readonly string[], b: readonly string[]): boolean => a.length === b.length && [...a].sort().join("\n") === [...b].sort().join("\n");

export async function generalize(o: {
  runner: ModelRunner; budget: Budget; models: { rewriter: string; judge: string }; proposal: Proposal; evidence: ReadonlyMap<string, string>;
}): Promise<Generalized> {
  const evidence = `Transcript lines behind the evidence (every line is fenced):\n${evidenceBlock(o.evidence)}`;
  const rewrite = await askModel(o.runner, o.budget, {
    role: GENERALIZE_ROLES.rewrite, model: o.models.rewriter, system: REWRITE_SYSTEM, schema: PROPOSAL_SCHEMA, parse: (v) => ProposalSchema.parse(v), timeoutMs: 600_000,
    input: `<untrusted id="proposal">${esc(JSON.stringify(o.proposal))}</untrusted>\n\n${evidence}`,
  });
  if (!rewrite.ok) return { ok: false, why: "generalize: the rewrite failed" };
  const p = rewrite.value;
  if (p.artifact !== o.proposal.artifact || p.kind !== o.proposal.kind || !sameRefs(p.evidence, o.proposal.evidence)) {
    return { ok: false, why: "generalize: the rewrite changed the artifact, kind or evidence" };
  }
  const judged = await askModel(o.runner, o.budget, {
    role: GENERALIZE_ROLES.judge, model: o.models.judge, system: JUDGE_SYSTEM, schema: VERDICT_SCHEMA, parse: (v) => Verdict.parse(v), timeoutMs: 600_000,
    input: `<untrusted id="candidate">${esc(JSON.stringify(p))}</untrusted>\n\n${evidence}`,
  });
  if (!judged.ok) return { ok: false, why: "generalize: the judge failed" };
  if (judged.value.leaks) return { ok: false, why: `generalize: the judge found ${judged.value.category}` };
  return { ok: true, proposal: p, category: judged.value.category, model: o.models.rewriter };
}
