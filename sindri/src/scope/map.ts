import { z } from "zod";
import { zodToJsonSchema } from "zod-to-json-schema";

import { displayRef, sanitizeIngest, type RefTable } from "./source.js";

export const SURFACE_KINDS = ["ui", "api", "job", "data", "integration", "permission", "report", "notification", "mobile", "flag", "other"] as const;
export const IMPLICATION_KINDS = ["migration", "permissions", "reporting", "notifications", "mobile", "flags", "other"] as const;

const Ref = z.string().regex(/^R\d+$/);
const SurfaceId = z.string().regex(/^S\d+$/);
const WorkstreamId = z.string().regex(/^W\d+$/);
const SurfaceSchema = z.object({ id: SurfaceId, kind: z.enum(SURFACE_KINDS), title: z.string().min(1).max(200), detail: z.string().max(2000), citations: z.array(Ref) });

export const ScopeMapSchema = z.object({
  subject: z.string().min(1).max(200),
  surfaces: z.array(SurfaceSchema).max(200),
  implications: z.array(z.object({ kind: z.enum(IMPLICATION_KINDS), detail: z.string().max(2000), citations: z.array(Ref) })).max(100),
  workstreams: z.array(z.object({
    id: WorkstreamId, title: z.string().min(1).max(200), surfaces: z.array(SurfaceId), dependsOn: z.array(WorkstreamId), acceptance: z.array(z.string().max(500)),
  })).max(50),
  questions: z.array(z.object({ question: z.string().min(1).max(500), options: z.array(z.string().max(200)), citations: z.array(Ref) })).max(50),
});

export type ScopeMap = z.infer<typeof ScopeMapSchema>;
export type Surface = ScopeMap["surfaces"][number];
type Workstream = ScopeMap["workstreams"][number];

export function scopeMapJsonSchema(): Record<string, unknown> {
  return zodToJsonSchema(ScopeMapSchema, { $refStrategy: "none" }) as Record<string, unknown>;
}

function findCycle(ws: Workstream[]): string[] | null {
  const byId = new Map(ws.map((w) => [w.id, w]));
  const state = new Map<string, "visiting" | "done">();
  const stack: string[] = [];
  const visit = (w: Workstream): string[] | null => {
    if (state.get(w.id) === "done") return null;
    if (state.get(w.id) === "visiting") return [...stack.slice(stack.indexOf(w.id)), w.id];
    state.set(w.id, "visiting");
    stack.push(w.id);
    // Unknown ids are reported by checkMap separately; only known workstreams are walked.
    for (const next of w.dependsOn.flatMap((d) => byId.get(d) ?? [])) {
      const c = visit(next);
      if (c !== null) return c;
    }
    stack.pop();
    state.set(w.id, "done");
    return null;
  };
  for (const w of ws) {
    const c = visit(w);
    if (c !== null) return c;
  }
  return null;
}

// Spec §7.5 step 1 and the §6 Scoping row: deterministic, so a model can't talk its way past them.
export function checkMap(map: ScopeMap, refs: RefTable): string[] {
  const reasons: string[] = [];
  if (map.surfaces.length === 0) reasons.push("map has no surfaces");
  const seen = new Set<string>();
  const known = new Set(refs.ids());
  const inStream = new Set(map.workstreams.flatMap((w) => w.surfaces));
  const streamCount = new Map<string, number>();
  for (const w of map.workstreams) for (const id of new Set(w.surfaces)) streamCount.set(id, (streamCount.get(id) ?? 0) + 1);
  const dupStream = new Set<string>();
  for (const s of map.surfaces) {
    if (seen.has(s.id)) reasons.push(`duplicate surface id ${s.id}`);
    seen.add(s.id);
    if (s.citations.length === 0) reasons.push(`surface ${s.id} cites no source`);
    for (const c of s.citations) if (!known.has(c)) reasons.push(`surface ${s.id} cites ${c}, which is not a source reference`);
    if (!inStream.has(s.id)) reasons.push(`surface ${s.id} is in no workstream`);
    if (!dupStream.has(s.id) && (streamCount.get(s.id) ?? 0) > 1) reasons.push(`surface ${s.id} is in more than one workstream`);
    dupStream.add(s.id);
  }
  map.implications.forEach((im, i) => {
    if (im.citations.length === 0) reasons.push(`implication ${i + 1} (${im.kind}) cites no source`);
    for (const c of im.citations) if (!known.has(c)) reasons.push(`implication ${i + 1} cites ${c}, which is not a source reference`);
  });
  map.questions.forEach((q, i) => {
    for (const c of q.citations) if (!known.has(c)) reasons.push(`question ${i + 1} cites ${c}, which is not a source reference`);
  });
  const streamIds = new Set(map.workstreams.map((w) => w.id));
  const seenStreams = new Set<string>();
  for (const w of map.workstreams) {
    if (seenStreams.has(w.id)) reasons.push(`duplicate workstream id ${w.id}`);
    seenStreams.add(w.id);
    for (const s of w.surfaces) if (!seen.has(s)) reasons.push(`workstream ${w.id} lists unknown surface ${s}`);
    for (const d of w.dependsOn) if (!streamIds.has(d)) reasons.push(`workstream ${w.id} depends on unknown workstream ${d}`);
    if (w.acceptance.length === 0) reasons.push(`workstream ${w.id} has no acceptance checks`);
  }
  const cycle = findCycle(map.workstreams);
  if (cycle !== null) reasons.push(`workstreams have a dependency cycle: ${cycle.join(" -> ")}`);
  return reasons;
}

// Spec §8.3: the model read untrusted text, so nothing it (or a source) wrote may
// become active content when a person opens the file. Order matters: strip first,
// then neutralize what is left.
export function safeText(s: string): string {
  return sanitizeIngest(s)
    .replace(/https?:\/\//gi, (m) => `hxx${m.slice(3)}`)
    .replace(/ftp:\/\//gi, "fxp://")
    // Backslash first, so a planted `\!` can't turn our own escape into a live one.
    .replace(/[\\<>[\]!`|*_~]/g, (c) => (c === "<" ? "&lt;" : c === ">" ? "&gt;" : `\\${c}`))
    .replace(/(javascript|vbscript|data):/gi, "$1\\:")
    .replace(/(?<!:)\/\//g, "/\\/")
    .replace(/www\./gi, "www\\.")
    .replace(/@/g, "\\@")
    .replace(/\s*[\r\n\u0085\u2028\u2029]\s*/g, " ")
    .trim();
}

export interface RenderMeta {
  status: "complete" | "incomplete";
  rounds: number;
  tokens: number;
  generatedAt: string;
  reasons: string[];
  notes: string[];
  added: number;
}

const list = (items: string[]): string[] => (items.length === 0 ? ["- none"] : items.map((i) => `- ${i}`));
const cites = (c: string[]): string => (c.length === 0 ? "none" : c.join(", "));

export function renderMap(map: ScopeMap, refs: RefTable, meta: RenderMeta): string {
  const runNotes = [
    ...meta.reasons.map((r) => `${meta.status === "incomplete" ? "Not verified" : "Note"}: ${safeText(r)}`),
    ...meta.notes.map((n) => `Source: ${safeText(n)}`),
  ];
  const lines = [
    `# Scope map: ${safeText(map.subject)}`,
    "",
    `Status: ${meta.status} · rounds: ${meta.rounds} · tokens: ${meta.tokens} · generated ${meta.generatedAt}`,
    "",
    "Surfaces, details and workstreams are model-drafted; check them against the cited sources. Sources marked untrusted came from comments, issues, transcripts or code.",
    ...(meta.added > 0 ? ["", `Surfaces added by the challenger: ${meta.added}`] : []),
    "",
    ...(runNotes.length > 0 ? ["## Run notes", "", ...runNotes.map((n) => `- ${n}`), ""] : []),
    "## Surfaces",
    "",
    ...map.surfaces.map((s) => `- **${s.id}** (${s.kind}) ${safeText(s.title)}${s.detail === "" ? "" : `: ${safeText(s.detail)}`}. Sources: ${cites(s.citations)}.`),
    "",
    "## Implications",
    "",
    ...list(map.implications.map((im) => `**${im.kind}:** ${safeText(im.detail)}. Sources: ${cites(im.citations)}.`)),
    "",
    "## Workstreams",
    "",
    ...map.workstreams.flatMap((w) => [
      `- **${w.id}** ${safeText(w.title)}`,
      `  - surfaces: ${w.surfaces.join(", ")}`,
      `  - depends on: ${w.dependsOn.length === 0 ? "none" : w.dependsOn.join(", ")}`,
      "  - acceptance:",
      ...list(w.acceptance.map((a) => safeText(a))).map((l) => `    ${l}`),
    ]),
    "",
    "## Open questions",
    "",
    ...list(map.questions.map((q) => `${safeText(q.question)} (options: ${q.options.length === 0 ? "open-ended" : q.options.map((o) => safeText(o)).join(" / ")}; sources: ${cites(q.citations)})`)),
    "",
    "## Sources",
    "",
    "| Ref | Kind | Trust | Author | Title | Reference | Excerpt |",
    "|---|---|---|---|---|---|---|",
    ...refs.entries().map(
      ([id, r]) => `| ${id} | ${r.kind} | ${r.trust} | ${safeText(r.author ?? "unknown")} | ${safeText(r.title)} | ${safeText(displayRef(r.ref))} | ${safeText(r.text.slice(0, 120))} |`,
    ),
    "",
  ];
  return lines.join("\n");
}

export function renderIncomplete(title: string, meta: RenderMeta): string {
  return [
    `# Scope map: ${safeText(title)}`,
    "",
    `Status: incomplete · rounds: ${meta.rounds} · tokens: ${meta.tokens} · generated ${meta.generatedAt}`,
    "",
    "No scope map passed the checks.",
    "",
    "## Why incomplete",
    "",
    ...list(meta.reasons.map((r) => safeText(r))),
    "",
    "## Source notes",
    "",
    ...list(meta.notes.map((n) => safeText(n))),
    "",
  ].join("\n");
}
