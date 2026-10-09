import { z } from "zod";
import { zodToJsonSchema } from "zod-to-json-schema";

import type { Budget, ModelRunner } from "../scope/model.js";
import { makeScrubber } from "../scrub/scrub.js";
import { askModel } from "./ask.js";
import { mentionsHoldout } from "./corpus.js";
import { TRANSCRIPTS_CLAUSE } from "./prompts.js";
import { parseEach, ProposalSchema, type Proposal } from "./proposals.js";
import type { Artifact } from "./registry.js";
import { readRepoSessions } from "./transcripts.js";

// Port of pstack `reflect` (MIT, © 2026 Lauren Tan): three reviewers + a synthesizer. The approval
// gate is replaced by the spec §7.4 tiers; the output is proposals, never repo edits.
const scrubber = makeScrubber();
const esc = (s: string): string => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

export function branchTranscript(dir: string, repo: string, branch: string, o: { cap: number; since: Date; dropTitles: readonly string[] }): string {
  const { lines } = readRepoSessions(dir, repo, o.since);
  const turns: string[] = [];
  for (const l of lines) {
    if (l.branch !== branch || (l.type !== "user" && l.type !== "assistant")) continue;
    const text = l.blocks.filter((b) => !b.toolResult).map((b) => b.text).join("\n").trim();
    if (text === "" || (l.type === "user" && (l.meta || text.startsWith("<"))) || mentionsHoldout(text, o.dropTitles)) continue;
    turns.push(`<untrusted id="${l.ref}" role="${l.type === "user" ? "human" : "assistant"}">${esc(scrubber.scrub(text).text)}</untrusted>`);
  }
  let total = turns.reduce((n, t) => n + t.length + 1, 0);
  while (turns.length > 0 && total > o.cap) {
    total -= turns[0].length + 1;
    turns.shift();
  }
  return turns.join("\n");
}

const Findings = z.object({
  findings: z.array(z.object({ title: z.string().max(200), evidence: z.array(z.string().max(100)).max(10), suggestion: z.string().max(1000), artifact: z.string().max(80) })).max(20),
});
const Note = z.object({ title: z.string().max(200), why: z.string().max(500) });
// Items are validated one at a time (parseEach); this loose shape only checks the three lists exist.
const SynthesisParse = z.object({ accepted: z.array(z.unknown()).max(10), rejected: z.array(Note).max(30), backlog: z.array(Note).max(30) });
const SynthesisShape = z.object({ accepted: z.array(ProposalSchema).max(10), rejected: z.array(Note).max(30), backlog: z.array(Note).max(30) });
const FINDINGS_SCHEMA = zodToJsonSchema(Findings, { $refStrategy: "none" }) as Record<string, unknown>;
const SYNTHESIS_SCHEMA = zodToJsonSchema(SynthesisShape, { $refStrategy: "none" }) as Record<string, unknown>;

export interface ReflectPrompts {
  judgment: string;
  tooling: string;
  divergent: string;
  synthesize: string;
}
export type ReflectNote = z.infer<typeof Note>;
export interface ReflectResult {
  accepted: Proposal[];
  rejected: ReflectNote[];
  backlog: ReflectNote[];
  incomplete: boolean;
  notes: string[];
}

export async function reflect(o: {
  runner: ModelRunner; models: { reviewer: string; synthesizer: string }; budget: Budget; prompts: ReflectPrompts;
  pr: { title: string; body: string; branch: string; files: string[]; diff: string }; transcript: string; artifacts: readonly Artifact[];
}): Promise<ReflectResult> {
  const notes: string[] = [];
  const input = [
    TRANSCRIPTS_CLAUSE,
    `<untrusted id="pr-title">${esc(o.pr.title)}</untrusted>`,
    `<untrusted id="pr-files">${esc(o.pr.files.join("\n"))}</untrusted>`,
    `<untrusted id="pr-body">${esc(o.pr.body)}</untrusted>`,
    `<untrusted id="pr-diff">${esc(o.pr.diff)}</untrusted>`,
    `Session transcript (every turn is fenced):\n${o.transcript}`,
    `Known artifacts: ${o.artifacts.map((a) => a.id).join(", ")}`,
  ].join("\n\n");
  const reviews: string[] = [];
  for (const role of ["judgment", "tooling", "divergent"] as const) {
    const a = await askModel(o.runner, o.budget, { role: "draft", model: o.models.reviewer, system: o.prompts[role], input, schema: FINDINGS_SCHEMA, parse: (v) => Findings.parse(v), timeoutMs: 600_000 });
    if (a.ok) reviews.push(`<untrusted id="reviewer-${role}">${esc(JSON.stringify(a.value))}</untrusted>`);
    else notes.push(`${role} reviewer: ${a.why}`);
  }
  if (reviews.length === 0) return { accepted: [], rejected: [], backlog: [], incomplete: true, notes: [...notes, "synthesizer: no reviewer produced findings"] };
  const s = await askModel(o.runner, o.budget, {
    role: "draft", model: o.models.synthesizer, system: o.prompts.synthesize, input: `${input}\n\nReviewer findings:\n${reviews.join("\n")}`, schema: SYNTHESIS_SCHEMA,
    parse: (v) => SynthesisParse.parse(v), timeoutMs: 600_000,
  });
  if (!s.ok) return { accepted: [], rejected: [], backlog: [], incomplete: true, notes: [...notes, `synthesizer: ${s.why}`] };
  const { ok, dropped } = parseEach(s.value.accepted);
  const known = new Set(o.artifacts.map((a) => a.id));
  return {
    accepted: ok.filter((p) => known.has(p.artifact)),
    rejected: [...s.value.rejected, ...dropped, ...ok.filter((p) => !known.has(p.artifact)).map((p) => ({ title: p.title, why: `unknown artifact ${p.artifact}` }))],
    backlog: s.value.backlog,
    incomplete: notes.length > 0,
    notes,
  };
}
