import { z } from "zod";
import { zodToJsonSchema } from "zod-to-json-schema";

import type { Budget, ModelRunner } from "../scope/model.js";
import { keywordsOf } from "../scope/source.js";
import type { Scrubber } from "../scrub/scrub.js";
import { askModel } from "./ask.js";
import { mentionsHoldout } from "./corpus.js";
import { TRANSCRIPTS_CLAUSE } from "./prompts.js";
import { parseEach, ProposalSchema, type Proposal } from "./proposals.js";
import type { Artifact } from "./registry.js";
import { readRepoSessions } from "./transcripts.js";

// Port of pstack `correct` (MIT, © 2026 Lauren Tan). A model labels each human turn; a keyword
// regex missed most corrections (it caught 5 of 45 wrong-approach ones, and about half of real
// corrections are about process, not design).
export const CORRECTION_LABELS = ["wrong_approach_design", "wrong_approach_process", "restate", "scope_surface"] as const;
export const LABELS = [...CORRECTION_LABELS, "rigor", "defect_report", "none"] as const;
export type CorrectionLabel = (typeof CORRECTION_LABELS)[number];
export type Label = (typeof LABELS)[number];

export interface CandidateTurn {
  ref: string;
  session: string;
  day: string;
  text: string;
  prevAssistantTail: string;
  editsBefore: boolean;
}

export interface Correction {
  ref: string;
  session: string;
  day: string;
  text: string;
  labels: CorrectionLabel[];
}

export interface Cluster {
  label: CorrectionLabel;
  items: Correction[];
}

export interface Labeled {
  corrections: Correction[];
  labeled: number;
  counts: Record<CorrectionLabel, number>;
  labelErrors: number;
  labelDropped: number;
  incomplete: boolean;
  notes: string[];
}

const esc = (s: string): string => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const EDIT_TOOLS = new Set(["Edit", "Write", "MultiEdit", "NotebookEdit"]);
const BATCH = 20;
const TAIL = 400;

const humanText = (blocks: { text: string; toolResult: boolean }[]): string => blocks.filter((b) => !b.toolResult).map((b) => b.text).join("\n").trim();

// The human turns of this repo's sessions, newest first, at most `cap`. readRepoSessions has already
// dropped lines copied into a forked or resumed session.
export function findCandidateTurns(dir: string, repo: string, since: Date, dropTitles: readonly string[], cap: number, scrubber: Scrubber): CandidateTurn[] {
  const { lines } = readRepoSessions(dir, repo, since);
  const lastAssistant = new Map<string, string>();
  const edited = new Set<string>();
  const found: { ts: string; turn: CandidateTurn }[] = [];
  for (const l of lines) {
    if (l.type === "assistant") {
      const text = humanText(l.blocks);
      if (text !== "") lastAssistant.set(l.file, text);
      if (l.tools.some((t) => EDIT_TOOLS.has(t))) edited.add(l.file);
      continue;
    }
    if (l.type !== "user" || !(Date.parse(l.ts) >= since.getTime())) continue;
    const text = humanText(l.blocks);
    const before = lastAssistant.get(l.file) ?? "";
    if (text === "" || text.startsWith("<") || mentionsHoldout(text, dropTitles)) continue;
    found.push({
      ts: l.ts,
      turn: {
        ref: l.ref, session: l.file, day: l.ts.slice(0, 10),
        text: scrubber.scrub(text).text.slice(0, 1500),
        prevAssistantTail: mentionsHoldout(before, dropTitles) ? "" : scrubber.scrub(before).text.slice(-TAIL),
        editsBefore: edited.has(l.file),
      },
    });
  }
  return found.sort((a, b) => Date.parse(b.ts) - Date.parse(a.ts)).slice(0, cap).map((f) => f.turn);
}

// Same definitions as Plan 1's labeler (copied, not imported from scorer).
export const LABEL_SYSTEM = [
  "You label human turns from coding-agent sessions. For each turn you get the human's text, the end of the assistant message just before it when there is one, and whether the assistant had already edited files earlier in that session.",
  "Return the labels that apply to each turn. A turn may have several labels, except `none`, which stands alone. Answer once for every turn id, using exactly the ids you were given.",
  "Labels:",
  "- wrong_approach_design: the human says the agent's technical approach or design is wrong.",
  "- wrong_approach_process: the human corrects how work is done or where it goes (CI vs local, which doc or tool, the order of steps), not the design.",
  "- defect_report: reports a concrete bug in the produced work.",
  "- restate: repeats an instruction already given or already in the ticket.",
  "- rigor: demands evidence, verification or certainty.",
  "- scope_surface: points at missed places or surfaces.",
  "- none: none of the above (a new request, a question, thanks, an answer).",
].join("\n");

const LabelItem = z
  .object({
    ref: z.string(),
    labels: z
      .array(z.enum(LABELS))
      .min(1)
      .refine((l) => new Set(l).size === l.length, "labels must be distinct")
      .refine((l) => !l.includes("none") || l.length === 1, "none stands alone"),
  })
  .strict();
const LABEL_SCHEMA = zodToJsonSchema(z.object({ results: z.array(LabelItem) }).strict(), { $refStrategy: "none" }) as Record<string, unknown>;

// Items are validated one by one: a bad item (unknown or repeated turn id, bad labels) is dropped and
// counted, the rest of the batch is kept. The batch fails only when the answer is not a list or no item parses.
const batchAnswer = (refs: readonly string[]) => (v: unknown): { results: z.infer<typeof LabelItem>[]; dropped: number } => {
  const items = z.object({ results: z.array(z.unknown()) }).parse(v).results;
  const seen = new Set<string>();
  const results: z.infer<typeof LabelItem>[] = [];
  for (const raw of items) {
    const r = LabelItem.safeParse(raw);
    if (!r.success || !refs.includes(r.data.ref) || seen.has(r.data.ref)) continue;
    seen.add(r.data.ref);
    results.push(r.data);
  }
  if (results.length === 0) throw new Error("no item of the answer labels a turn id it was given");
  return { results, dropped: items.length - results.length };
};

const fenceTurn = (t: CandidateTurn): string =>
  [
    `<untrusted id="${t.ref}" kind="human" edits-before="${t.editsBefore ? "yes" : "no"}">${esc(t.text)}</untrusted>`,
    ...(t.prevAssistantTail === "" ? [] : [`<untrusted id="${t.ref}" kind="previous-assistant">${esc(t.prevAssistantTail)}</untrusted>`]),
  ].join("\n");

// Labels the turns in batches of 20. A bad batch is retried once and then counted in labelErrors
// (it never aborts the run); an exhausted budget stops the loop with a partial result.
export async function labelTurns(turns: readonly CandidateTurn[], o: { runner: ModelRunner; model: string; budget: Budget }): Promise<Labeled> {
  const out: Labeled = {
    corrections: [], labeled: 0, labelErrors: 0, labelDropped: 0, incomplete: false, notes: [],
    counts: { wrong_approach_design: 0, wrong_approach_process: 0, restate: 0, scope_surface: 0 },
  };
  for (let start = 0; start < turns.length; start += BATCH) {
    if (o.budget.exhausted()) {
      out.incomplete = true;
      out.notes.push(`labeling stopped before batch ${start / BATCH + 1}: token budget exhausted`);
      break;
    }
    const batch = turns.slice(start, start + BATCH);
    const refs = batch.map((t) => t.ref);
    const call = {
      role: "label", model: o.model, system: LABEL_SYSTEM, input: [TRANSCRIPTS_CLAUSE, ...batch.map(fenceTurn)].join("\n\n"),
      schema: LABEL_SCHEMA, parse: batchAnswer(refs), timeoutMs: 600_000,
    };
    let a = await askModel(o.runner, o.budget, call);
    if (!a.ok) a = await askModel(o.runner, o.budget, call);
    if (!a.ok) {
      out.labelErrors += 1;
      continue;
    }
    out.labelDropped += a.value.dropped;
    const labelsOf = new Map<string, readonly Label[]>(a.value.results.map((r) => [r.ref, r.labels] as const));
    for (const t of batch) {
      const got = labelsOf.get(t.ref);
      if (got === undefined) continue;
      out.labeled += 1;
      const kept = CORRECTION_LABELS.filter((l) => got.includes(l));
      if (kept.length === 0) continue;
      for (const l of kept) out.counts[l] += 1;
      out.corrections.push({ ref: t.ref, session: t.session, day: t.day, text: t.text, labels: kept });
    }
  }
  return out;
}

function overlap(a: Set<string>, b: Set<string>): number {
  const inter = [...a].filter((x) => b.has(x)).length;
  const union = new Set([...a, ...b]).size;
  return union === 0 ? 0 : inter / union;
}

// Single-link clusters over keyword sets, kept only with two sessions and two days.
function singleLink(cs: Correction[]): Correction[][] {
  const keys = cs.map((x) => new Set(keywordsOf(x.text, 8)));
  const parent = cs.map((_, i) => i);
  const find = (i: number): number => (parent[i] === i ? i : (parent[i] = find(parent[i])));
  for (let i = 0; i < cs.length; i++) for (let j = i + 1; j < cs.length; j++) if (overlap(keys[i], keys[j]) >= 0.3) parent[find(j)] = find(i);
  const groups = new Map<number, Correction[]>();
  cs.forEach((x, i) => groups.set(find(i), [...(groups.get(find(i)) ?? []), x]));
  return [...groups.values()].filter((g) => new Set(g.map((x) => x.session)).size >= 2 && new Set(g.map((x) => x.day)).size >= 2);
}

// Group by the first label (in CORRECTION_LABELS order) first, then cluster within each group.
export function clusterCorrections(cs: Correction[]): Cluster[] {
  return CORRECTION_LABELS.flatMap((label) =>
    singleLink(cs.filter((x) => CORRECTION_LABELS.find((l) => x.labels.includes(l)) === label)).map((items) => ({ label, items })),
  );
}

const Answer = z.object({ proposal: z.unknown() }).refine((v) => v.proposal !== undefined, "proposal is required");
const ANSWER_SCHEMA = zodToJsonSchema(z.object({ proposal: ProposalSchema }), { $refStrategy: "none" }) as Record<string, unknown>;

export async function correct(o: {
  runner: ModelRunner; model: string; budget: Budget; prompt: string; clusters: Cluster[]; artifacts: readonly Artifact[];
}): Promise<{ proposals: Proposal[]; dropped: { title: string; why: string }[]; incomplete: boolean; notes: string[] }> {
  const known = new Set(o.artifacts.map((a) => a.id));
  const proposals: Proposal[] = [];
  const dropped: { title: string; why: string }[] = [];
  const notes: string[] = [];
  for (const [i, cluster] of o.clusters.slice(0, 5).entries()) {
    const input = [
      TRANSCRIPTS_CLAUSE,
      `Class label: ${cluster.label}`,
      ...cluster.items.map((x) => `<untrusted id="${x.ref}" labels="${x.labels.join(",")}">${esc(x.text)}</untrusted>`),
      `Known artifacts: ${o.artifacts.map((a) => a.id).join(", ")}`,
    ].join("\n\n");
    const a = await askModel(o.runner, o.budget, { role: "draft", model: o.model, system: o.prompt, input, schema: ANSWER_SCHEMA, parse: (v) => Answer.parse(v), timeoutMs: 600_000 });
    if (!a.ok) {
      notes.push(`class ${i + 1}: ${a.why}`);
      continue;
    }
    const parsed = parseEach([a.value.proposal]);
    dropped.push(...parsed.dropped);
    for (const p of parsed.ok) {
      if (known.has(p.artifact)) proposals.push(p);
      else dropped.push({ title: p.title, why: `unknown artifact ${p.artifact}` });
    }
  }
  return { proposals, dropped, incomplete: notes.length > 0, notes };
}
