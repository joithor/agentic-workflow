import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import type { GitRunner } from "../git.js";
import { ulid } from "../ids.js";
import type { Ledger } from "../ledger/db.js";
import { terminalSafe } from "./invisible.js";
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
export const OPEN: readonly ProposalStatus[] = STATUSES.filter((s) => !TERMINAL.includes(s));

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

const STRICTNESS: Record<Tier, number> = { "self-adopt": 0, code: 1, approval: 2 };

// The tier a public version ships at: the stricter of the original's and the rewrite's. The rewrite model sees
// untrusted transcript text and may change the file list, so a rewrite can raise the tier, never lower it.
export function publicTier(original: Proposal, pub: Proposal, artifacts: readonly Artifact[], extraProtected: readonly string[] = []): { tier: Tier; why: string } {
  const a = classifyTier(original, artifacts, extraProtected);
  const b = classifyTier(pub, artifacts, extraProtected);
  return STRICTNESS[a.tier] > STRICTNESS[b.tier] ? a : b;
}

const scrubber = makeScrubber();
// NFKC, lower case, whitespace to one space, then only letters, digits and spaces: punctuation and zero-width variants dedupe.
export const normTitle = (t: string): string => t.normalize("NFKC").toLowerCase().replace(/\s+/g, " ").replace(/[^\p{L}\p{N} ]/gu, "").replace(/ +/g, " ").trim();

export type SaveOutcome = { kind: "saved" | "duplicate" | "previously-rejected"; id: string };

// `sessions`: the transcript sessions (prefixes as sessionOf gives them) whose text the caller sent to the model for
// this proposal, decided by code, never read from the model's answer (security-sentinel-0). Only ever added to: a
// re-proposal merged into an open one with recorded sources adds its sessions, so the verbatim layer checks every source it was built from.
export function saveProposal(db: Ledger, p: Proposal, source: string, tier: Tier, epoch: number, now: Date, sessions: readonly string[] = []): SaveOutcome {
  const clean = scrubber.scrubDeep(p);
  const norm = normTitle(clean.title);
  const prior = db.prepare("SELECT id, status, body FROM proposals WHERE artifact_id = ? COLLATE NOCASE AND norm_title = ? ORDER BY created_at DESC, id DESC").all(p.artifact, norm) as { id: string; status: ProposalStatus; body: string }[];
  const open = prior.find((r) => !TERMINAL.includes(r.status));
  const ts = now.toISOString();
  const addSources = (id: string): void => {
    const add = db.prepare("INSERT OR IGNORE INTO proposal_sources (proposal_id, session) VALUES (?, ?)");
    for (const s of new Set(sessions)) add.run(id, s);
  };
  if (open !== undefined) {
    const older = ProposalSchema.parse(JSON.parse(open.body));
    const merged = { ...older, evidence: [...new Set([...older.evidence, ...clean.evidence])].slice(-20) };
    db.prepare("UPDATE proposals SET body = ?, updated_at = ?, epoch = ? WHERE id = ?").run(JSON.stringify(merged), ts, epoch, open.id);
    // Only into a proposal whose sources were recorded (NO_TRANSCRIPT counts): one saved before provenance existed keeps
    // its original text, drafted from sessions nobody recorded, so it stays unrecorded and held (run 4
    // kieran-typescript-reviewer-1, security-sentinel-3).
    if (proposalSources(db, open.id).length > 0) addSources(open.id);
    return { kind: "duplicate", id: open.id };
  }
  const rejected = prior.find((r) => r.status === "rejected");
  if (rejected !== undefined) return { kind: "previously-rejected", id: rejected.id };
  const id = ulid(now);
  db.prepare(
    "INSERT INTO proposals (id, artifact_id, source, kind, tier, status, title, norm_title, body, created_at, updated_at, epoch) VALUES (?, ?, ?, ?, ?, 'proposed', ?, ?, ?, ?, ?, ?)",
  ).run(id, p.artifact, scrubber.scrub(source).text, p.kind, tier, clean.title, norm, JSON.stringify(clean), ts, ts, epoch);
  addSources(id);
  return { kind: "saved", id };
}

// The source row a transcript-reading command records when the model read no transcript text at all (reflect on a PR
// with no session on its branch). Never a session prefix (sessionOf gives letters, digits and dots only), so nothing
// resolves through it; it tells publish that provenance was recorded, and there was none.
export const NO_TRANSCRIPT = "-";

// The recorded source sessions of a proposal, sorted (NO_TRANSCRIPT included when it was recorded).
export const proposalSources = (db: Ledger, id: string): string[] =>
  (db.prepare("SELECT session FROM proposal_sources WHERE proposal_id = ? ORDER BY session").all(id) as { session: string }[]).map((r) => r.session);

// Whether a proposal's source fed transcript text to the model: every source but telemetry, whose text is fixed
// (fail closed: an unknown source kind counts as reading transcripts).
export const readsTranscripts = (source: string): boolean => !source.startsWith("telemetry:");

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

// The generalized version publish renders (privacy spec 5.1). The original stays in `proposals`, local only.
// INSERT OR REPLACE on purpose: re-generalizing a proposal (a held retry, a v4-era staged row) replaces its row.
// Returns the version as stored (scrubbed, decoded from the stored body): stage renders its preview from it, so the
// preview and publish read one source. Null, and nothing stored, when the scrubbed version no longer parses (a
// redaction inside a file path, say): the caller holds the proposal.
export function savePublic(db: Ledger, id: string, p: Proposal, o: { category: string; model: string }, epoch: number, now: Date): Proposal | null {
  const parsed = ProposalSchema.safeParse(scrubber.scrubDeep(p));
  if (!parsed.success) return null;
  const body = JSON.stringify(parsed.data);
  db.prepare("INSERT OR REPLACE INTO proposal_public (proposal_id, body, judge_category, model, created_at, epoch) VALUES (?, ?, ?, ?, ?, ?)")
    .run(id, body, o.category, o.model, now.toISOString(), epoch);
  return ProposalSchema.parse(JSON.parse(body));
}

// A proposal's public row: none, one that parses, or one that no longer does (a body written by another build, or edited
// by hand). Never throws: publish holds an unreadable one and stage regenerates it like a missing one
// (kieran-typescript-reviewer-1).
export type PublicRow = { state: "none" } | { state: "ok"; value: Proposal } | { state: "unreadable" };

export function readPublic(db: Ledger, id: string): PublicRow {
  const r = db.prepare("SELECT body FROM proposal_public WHERE proposal_id = ?").get(id) as { body: string } | undefined;
  if (r === undefined) return { state: "none" };
  let raw: unknown;
  try {
    raw = JSON.parse(r.body);
  } catch {
    return { state: "unreadable" };
  }
  const parsed = ProposalSchema.safeParse(raw);
  return parsed.success ? { state: "ok", value: parsed.data } : { state: "unreadable" };
}

// The public version when its row parses; null when there is none or it is unreadable.
export function getPublic(db: Ledger, id: string): Proposal | null {
  const r = readPublic(db, id);
  return r.state === "ok" ? r.value : null;
}

// Whether a public row exists, without reading its body: a body that no longer parses can't make the check throw.
export const hasPublic = (db: Ledger, id: string): boolean => db.prepare("SELECT 1 FROM proposal_public WHERE proposal_id = ? LIMIT 1").get(id) !== undefined;

export function listProposals(db: Ledger, statuses?: readonly ProposalStatus[]): { id: string; artifact: string; tier: Tier; status: ProposalStatus; title: string; source: string; createdAt: string }[] {
  const where = statuses === undefined ? "" : `WHERE status IN (${statuses.map(() => "?").join(",")})`;
  const rows = db.prepare(`SELECT id, artifact_id, tier, status, title, source, created_at FROM proposals ${where} ORDER BY created_at, id`).all(...(statuses ?? [])) as
    { id: string; artifact_id: string; tier: Tier; status: ProposalStatus; title: string; source: string; created_at: string }[];
  return rows.map((r) => ({ id: r.id, artifact: r.artifact_id, tier: r.tier, status: r.status, title: r.title, source: r.source, createdAt: r.created_at }));
}

export function listStored(db: Ledger, statuses: readonly ProposalStatus[]): StoredProposal[] {
  const ids = (db.prepare(`SELECT id FROM proposals WHERE status IN (${statuses.map(() => "?").join(",")}) ORDER BY created_at, id`).all(...statuses) as { id: string }[]).map((r) => r.id);
  return ids.map((id) => getProposal(db, id)).filter((s): s is StoredProposal => s !== null);
}

// The one place a status changes: a compare-and-set. The UPDATE applies only while the proposal is still in one of
// the `from` statuses, so a stale read (made before the lock, or before another terminal committed) can't resurrect
// a rejected proposal or overwrite adopted. False means it did not apply; callers skip what depends on it.
export function transition(db: Ledger, id: string, from: readonly ProposalStatus[], to: ProposalStatus, epoch: number, now: Date): boolean {
  const marks = from.map(() => "?").join(",");
  return db.prepare(`UPDATE proposals SET status = ?, updated_at = ?, epoch = ? WHERE id = ? AND status IN (${marks})`).run(to, now.toISOString(), epoch, id, ...from).changes > 0;
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

export const SummaryDetail = z.object({ line: z.string() });

// The latest finished comparison (item_id '*'): the highest run number, not the last row to finish, and never
// a `running` marker, which carries no summary.
export function latestComparison(db: Ledger, id: string): { run: number; status: string; line: string } | null {
  const r = db.prepare("SELECT run, verdict, detail FROM comparisons WHERE proposal_id = ? AND item_id = '*' AND verdict != 'running' ORDER BY run DESC, seq DESC LIMIT 1").get(id) as
    | { run: number; verdict: string; detail: string }
    | undefined;
  return r === undefined ? null : { run: r.run, status: r.verdict, line: SummaryDetail.parse(JSON.parse(r.detail)).line };
}

// How many comparison runs the proposal has had, finished or still running.
export function comparisonRuns(db: Ledger, id: string): number {
  return (db.prepare("SELECT COUNT(DISTINCT run) AS c FROM comparisons WHERE proposal_id = ? AND item_id = '*'").get(id) as { c: number }).c;
}

// The run number of a comparison that has started and not closed (no summary row after its `running` marker), or null.
export function runningComparison(db: Ledger, id: string): number | null {
  const r = db.prepare(
    "SELECT run FROM comparisons m WHERE proposal_id = ? AND item_id = '*' AND verdict = 'running' AND NOT EXISTS (SELECT 1 FROM comparisons c WHERE c.proposal_id = m.proposal_id AND c.run = m.run AND c.item_id = '*' AND c.verdict != 'running') ORDER BY run DESC LIMIT 1",
  ).get(id) as { run: number } | undefined;
  return r === undefined ? null : r.run;
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

// Held by stage's generalization pass rather than by publish's gate: status held and no readable public version. The one
// place this is decided (show, retry).
export const heldByGeneralize = (db: Ledger, s: { id: string; status: ProposalStatus }): boolean => s.status === "held" && readPublic(db, s.id).state !== "ok";

// `generalize`: for a proposal held by the generalization pass, its content holds and evolve.maxGeneralizeAttempts. Under
// the cap stage retries it; at the cap stage gave up, and retry is the way out that keeps its title usable
// (architecture-strategist-0). A proposal held by publish's gate is reworded by rejecting it.
export function nextFor(s: { id: string; status: ProposalStatus; tier: Tier }, generalize: { holds: number; max: number } | null = null): string {
  if (s.status === "proposed") return s.tier === "self-adopt" ? `sindri evolve compare ${s.id}` : "sindri evolve stage";
  if (s.status === "won") return `sindri evolve adopt ${s.id}`;
  if (s.status === "staged") return "sindri evolve publish";
  if (s.status === "held") {
    if (generalize === null) return `sindri evolve reject ${s.id} --reason "..."`;
    return generalize.holds >= generalize.max ? `sindri evolve retry ${s.id} (or sindri evolve reject ${s.id} --reason "...")` : "sindri evolve stage";
  }
  if (s.status === "published" || s.status === "insufficient-corpus") return "sindri evolve status";
  return "sindri evolve proposals";
}

const TIER_ORDER: readonly Tier[] = ["code", "approval", "self-adopt"];
const note = (o: SaveOutcome): string => (o.kind === "duplicate" ? " (already proposed)" : o.kind === "previously-rejected" ? " (rejected before)" : "");

// The shared summary of `reflect` and `correct`: tier counts in a fixed order, one line per outcome.
export function renderSaved(saved: readonly { title: string; tier: Tier; outcome: SaveOutcome }[]): { tiers: string[]; lines: string[] } {
  return {
    tiers: TIER_ORDER.map((t) => [t, saved.filter((s) => s.tier === t).length] as const).filter(([, n]) => n > 0).map(([t, n]) => `${n} ${t}`),
    lines: saved.map((s) => `  ${s.outcome.id}  ${s.tier.padEnd(8)}  ${terminalSafe(s.title)}${note(s.outcome)}`),
  };
}
