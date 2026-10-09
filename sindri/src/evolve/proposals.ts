import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import type { GitRunner } from "../git.js";
import { ulid } from "../ids.js";
import type { Ledger } from "../ledger/db.js";
import { makeScrubber } from "../scrub/scrub.js";
import { isEvalMachinery, isProtectedPath, normalizeRepoPath, type Artifact } from "./registry.js";

// Only this prompt has an offline comparison today (`evolve compare`); every other prompt is reviewed as code.
export const SELF_ADOPT_PROMPT = "prompt:scope.draft";
export const ARTIFACT_RE = /^(skill|hook|package|installer|rule|doc|mod|pack-pin|prompt):[A-Za-z0-9._-]+$/;

// A file a model names: validated and normalized here, so nothing downstream sees a raw string.
const RepoPath = z.string().max(200).transform((p, ctx) => {
  const n = normalizeRepoPath(p);
  if (n === null) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: "must be a repo-relative path (letters, digits, . _ - /; no .. and no leading /)" });
    return z.NEVER;
  }
  return n;
});

export const ProposalSchema = z.object({
  artifact: z.string().regex(ARTIFACT_RE),
  kind: z.enum(["prompt-edit", "skill-edit", "hook-fix", "rule", "docs", "code"]),
  title: z.string().min(5).max(120),
  rationale: z.string().max(2000),
  evidence: z.array(z.string().max(200)).max(20),
  change: z.discriminatedUnion("type", [
    z.object({ type: z.literal("replace-prompt"), text: z.string().min(1).max(20000) }),
    z.object({ type: z.literal("describe"), files: z.array(RepoPath).min(1).max(10), description: z.string().max(4000) }),
  ]),
});

export type Proposal = z.infer<typeof ProposalSchema>;
export type Tier = "self-adopt" | "approval" | "code";
export type ProposalStatus = "proposed" | "evaluating" | "won" | "lost" | "insufficient-corpus" | "adopted" | "staged" | "held" | "published" | "merged" | "rejected";
export const STATUSES: readonly ProposalStatus[] = ["proposed", "evaluating", "won", "lost", "insufficient-corpus", "adopted", "staged", "held", "published", "merged", "rejected"];
export const TERMINAL: readonly ProposalStatus[] = ["rejected", "adopted", "lost", "merged"];

function titleOf(raw: unknown): string {
  const t = typeof raw === "object" && raw !== null ? (raw as { title?: unknown }).title : undefined;
  return typeof t === "string" && t.trim() !== "" ? t.slice(0, 120) : "(untitled)";
}

// One bad item in a model's answer is dropped with a reason; it never aborts the run.
export function parseEach(items: readonly unknown[]): { ok: Proposal[]; dropped: { title: string; why: string }[] } {
  const ok: Proposal[] = [];
  const dropped: { title: string; why: string }[] = [];
  for (const raw of items) {
    const r = ProposalSchema.safeParse(raw);
    if (r.success) ok.push(r.data);
    else dropped.push({ title: titleOf(raw), why: `invalid proposal: ${r.error.issues[0].path.join(".") || "(root)"}: ${r.error.issues[0].message}` });
  }
  return { ok, dropped };
}

export const owns = (a: Artifact, f: string): boolean => a.paths.includes(f) || (a.root !== null && f.startsWith(a.root));

// Spec §7.4 tiers + invariant 11 (no self-certification). Fails closed: anything unknown is approval, and a
// proposal may only touch files its own artifact owns. Paths are re-normalized here so an unvalidated
// proposal can't smuggle in a disguised protected path (`./x//y`, `a/../y`, other case).
export function classifyTier(p: Proposal, artifacts: readonly Artifact[], extraProtected: readonly string[] = []): { tier: Tier; why: string } {
  const a = artifacts.find((x) => x.id === p.artifact);
  if (a === undefined) return { tier: "approval", why: `${p.artifact} isn't in the registry (failing closed)` };
  if (a.protected) return { tier: "approval", why: "touches a protected artifact" };
  if (p.change.type === "replace-prompt") {
    if (a.kind !== "prompt") return { tier: "approval", why: "a prompt replacement on an artifact that isn't a prompt" };
    return a.id === SELF_ADOPT_PROMPT
      ? { tier: "self-adopt", why: "a prompt replacement, adopted only after winning the offline comparison" }
      : { tier: "approval", why: "no offline comparison yet; amendment 4" };
  }
  const files = p.change.files.map(normalizeRepoPath);
  if (files.some((f) => f === null)) return { tier: "approval", why: "names a path that can't be normalized" };
  const norm = files as string[];
  if (norm.some(isEvalMachinery)) return { tier: "approval", why: "changes the evaluation machinery that judges it (invariant 11)" };
  if (norm.some((f) => isProtectedPath(f, extraProtected))) return { tier: "approval", why: "touches a protected path" };
  if (norm.some((f) => !owns(a, f))) return { tier: "approval", why: `touches files outside ${a.id}` };
  return { tier: "code", why: "a repo change, built and merged as an ordinary work item" };
}

const scrubber = makeScrubber();
// NFKC, lower case, whitespace to one space, then only letters, digits and spaces: punctuation and zero-width variants dedupe.
export const normTitle = (t: string): string => t.normalize("NFKC").toLowerCase().replace(/\s+/g, " ").replace(/[^\p{L}\p{N} ]/gu, "").replace(/ +/g, " ").trim();

export type SaveOutcome = { kind: "saved" | "duplicate" | "previously-rejected"; id: string };

export function saveProposal(db: Ledger, p: Proposal, source: string, tier: Tier, epoch: number, now: Date): SaveOutcome {
  const clean = scrubber.scrubDeep(p);
  const norm = normTitle(clean.title);
  const prior = db.prepare("SELECT id, status, body FROM proposals WHERE artifact_id = ? COLLATE NOCASE AND norm_title = ? ORDER BY created_at DESC, id DESC").all(p.artifact, norm) as { id: string; status: ProposalStatus; body: string }[];
  const open = prior.find((r) => !TERMINAL.includes(r.status));
  const ts = now.toISOString();
  if (open !== undefined) {
    const older = ProposalSchema.parse(JSON.parse(open.body));
    const merged = { ...older, evidence: [...new Set([...older.evidence, ...clean.evidence])].slice(-20) };
    db.prepare("UPDATE proposals SET body = ?, updated_at = ?, epoch = ? WHERE id = ?").run(JSON.stringify(merged), ts, epoch, open.id);
    return { kind: "duplicate", id: open.id };
  }
  const rejected = prior.find((r) => r.status === "rejected");
  if (rejected !== undefined) return { kind: "previously-rejected", id: rejected.id };
  const id = ulid(now);
  db.prepare(
    "INSERT INTO proposals (id, artifact_id, source, kind, tier, status, title, norm_title, body, created_at, updated_at, epoch) VALUES (?, ?, ?, ?, ?, 'proposed', ?, ?, ?, ?, ?, ?)",
  ).run(id, p.artifact, scrubber.scrub(source).text, p.kind, tier, clean.title, norm, JSON.stringify(clean), ts, ts, epoch);
  return { kind: "saved", id };
}

export interface StoredProposal {
  id: string;
  artifact: string;
  source: string;
  tier: Tier;
  status: ProposalStatus;
  createdAt: string;
  updatedAt: string;
  proposal: Proposal;
}

export function getProposal(db: Ledger, id: string): StoredProposal | null {
  const r = db.prepare("SELECT id, artifact_id, source, tier, status, body, created_at, updated_at FROM proposals WHERE id = ?").get(id) as
    | { id: string; artifact_id: string; source: string; tier: Tier; status: ProposalStatus; body: string; created_at: string; updated_at: string }
    | undefined;
  return r === undefined
    ? null
    : { id: r.id, artifact: r.artifact_id, source: r.source, tier: r.tier, status: r.status, createdAt: r.created_at, updatedAt: r.updated_at, proposal: ProposalSchema.parse(JSON.parse(r.body)) };
}

export function listProposals(db: Ledger, statuses?: readonly ProposalStatus[]): { id: string; artifact: string; tier: Tier; status: ProposalStatus; title: string; source: string; createdAt: string }[] {
  const where = statuses === undefined ? "" : `WHERE status IN (${statuses.map(() => "?").join(",")})`;
  const rows = db.prepare(`SELECT id, artifact_id, tier, status, title, source, created_at FROM proposals ${where} ORDER BY created_at, id`).all(...(statuses ?? [])) as
    { id: string; artifact_id: string; tier: Tier; status: ProposalStatus; title: string; source: string; created_at: string }[];
  return rows.map((r) => ({ id: r.id, artifact: r.artifact_id, tier: r.tier, status: r.status, title: r.title, source: r.source, createdAt: r.created_at }));
}

export function setStatus(db: Ledger, id: string, status: ProposalStatus, epoch: number, now: Date): void {
  db.prepare("UPDATE proposals SET status = ?, updated_at = ?, epoch = ? WHERE id = ?").run(status, now.toISOString(), epoch, id);
}

export function setTier(db: Ledger, id: string, tier: Tier, epoch: number, now: Date): void {
  db.prepare("UPDATE proposals SET tier = ?, updated_at = ?, epoch = ? WHERE id = ?").run(tier, now.toISOString(), epoch, id);
}

// Proposals waiting for a human to merge: the cap (evolve.maxOpenProposals) applies to these. A `held`
// proposal (withheld by publish's privacy gate) is deliberately not counted: it can't ship until it is
// reworded or rejected, so it must not block new proposals.
export function inFlightCount(db: Ledger): number {
  return (db.prepare("SELECT COUNT(*) AS c FROM proposals WHERE status IN ('staged', 'published')").get() as { c: number }).c;
}

const SummaryDetail = z.object({ line: z.string() });

// The newest comparison summary row (item_id '*'), written by `evolve compare`.
export function latestComparison(db: Ledger, id: string): { run: number; status: string; line: string } | null {
  const r = db.prepare("SELECT run, verdict, detail FROM comparisons WHERE proposal_id = ? AND item_id = '*' ORDER BY seq DESC LIMIT 1").get(id) as
    | { run: number; verdict: string; detail: string }
    | undefined;
  return r === undefined ? null : { run: r.run, status: r.verdict, line: SummaryDetail.parse(JSON.parse(r.detail)).line };
}

// pr:<n> and transcript:<session-prefix>#<line> only: no project directory names, no free text.
export function reduceEvidence(refs: readonly string[]): { refs: string[]; withheld: number } {
  const out: string[] = [];
  let withheld = 0;
  for (const r of refs) {
    const pr = /^pr:(\d{1,6})$/.exec(r);
    const tr = /^(?:transcript:)?(?:[^#]*\/)?([A-Za-z0-9]{1,8})(?:\.(?!jsonl\b)([A-Za-z0-9]{1,40}))?[A-Za-z0-9-]*(?:\.jsonl)?#(\d{1,7})$/.exec(r);
    const clean = pr !== null ? `pr:${pr[1]}` : tr !== null ? `transcript:${tr[1]}${tr[2] === undefined ? "" : `.${tr[2]}`}#${tr[3]}` : null;
    if (clean === null) withheld++;
    else if (!out.includes(clean)) out.push(clean);
  }
  return { refs: out, withheld };
}

// Where `stage` writes a human-readable preview of one proposal (never inside the repo).
export const stagedFile = (deps: Deps, id: string): string => path.join(stateDir(deps), "proposals", "staged", `${id}.md`);

// A published proposal is merged once a commit on the default branch mentions Proposal `<id>`.
export async function findMerged(db: Ledger, git: GitRunner, repo: string, defaultBranch: string): Promise<string[]> {
  const ids = (db.prepare("SELECT id FROM proposals WHERE status = 'published' ORDER BY id").all() as { id: string }[]).map((r) => r.id);
  const found: string[] = [];
  for (const id of ids) {
    const r = await git.run(["log", "-1", "--format=%H", "--fixed-strings", `--grep=Proposal \`${id}\``, defaultBranch], repo);
    if (r.ok && r.stdout.trim() !== "") found.push(id);
  }
  return found;
}

export function nextFor(s: { id: string; status: ProposalStatus; tier: Tier }): string {
  if (s.status === "proposed") return s.tier === "self-adopt" ? `sindri evolve compare ${s.id}` : "sindri evolve stage";
  if (s.status === "won") return `sindri evolve adopt ${s.id}`;
  if (s.status === "staged") return "sindri evolve publish";
  if (s.status === "held") return `sindri evolve reject ${s.id} --reason "..."`;
  if (s.status === "published" || s.status === "insufficient-corpus") return "sindri evolve status";
  return "sindri evolve proposals";
}

const TIER_ORDER: readonly Tier[] = ["code", "approval", "self-adopt"];
const note = (o: SaveOutcome): string => (o.kind === "duplicate" ? " (already proposed)" : o.kind === "previously-rejected" ? " (rejected before)" : "");

// The shared summary of `reflect` and `correct`: tier counts in a fixed order, one line per outcome.
export function renderSaved(saved: readonly { title: string; tier: Tier; outcome: SaveOutcome }[]): { tiers: string[]; lines: string[] } {
  return {
    tiers: TIER_ORDER.map((t) => [t, saved.filter((s) => s.tier === t).length] as const).filter(([, n]) => n > 0).map(([t, n]) => `${n} ${t}`),
    lines: saved.map((s) => `  ${s.outcome.id}  ${s.tier.padEnd(8)}  ${s.title}${note(s.outcome)}`),
  };
}
