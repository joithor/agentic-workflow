import Database from "better-sqlite3";

export type Db = Database.Database;

export const MIGRATIONS = `
CREATE TABLE IF NOT EXISTS decisions (
  id TEXT PRIMARY KEY,
  ts TEXT NOT NULL,
  question TEXT NOT NULL,
  content_class TEXT NOT NULL,
  provider TEXT NOT NULL,
  decision TEXT,
  confidence REAL NOT NULL,
  reason_code TEXT NOT NULL,
  latency_ms INTEGER NOT NULL,
  input_digest TEXT NOT NULL,
  undone_at TEXT,
  chain_position INTEGER NOT NULL DEFAULT 0,
  skipped TEXT NOT NULL DEFAULT '[]',
  outcome TEXT NOT NULL DEFAULT 'decided'
);
CREATE INDEX IF NOT EXISTS decisions_ts ON decisions(ts);
CREATE INDEX IF NOT EXISTS decisions_question_ts ON decisions(question, ts);
CREATE TABLE IF NOT EXISTS failures (
  ts TEXT NOT NULL,
  question TEXT NOT NULL,
  provider TEXT NOT NULL,
  reason_code TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS failures_ts ON failures(ts);
CREATE TABLE IF NOT EXISTS briefs (
  tool_use_id TEXT PRIMARY KEY,
  agent_id TEXT,
  session_id TEXT NOT NULL,
  prompt_id TEXT NOT NULL,
  dispatch_name TEXT,
  subagent_type TEXT NOT NULL,
  goal TEXT NOT NULL,
  acceptance_criteria TEXT NOT NULL,
  proof_command TEXT NOT NULL,
  saved_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS briefs_agent_id ON briefs(agent_id);
CREATE INDEX IF NOT EXISTS briefs_dispatch_name ON briefs(dispatch_name, agent_id, saved_at);
CREATE INDEX IF NOT EXISTS briefs_dispatch_lookup ON briefs(session_id, prompt_id, subagent_type, agent_id, saved_at);
CREATE TABLE IF NOT EXISTS decision_details (
  id TEXT PRIMARY KEY,
  input_json TEXT NOT NULL,
  probabilities TEXT,
  rules_opinion TEXT,
  agreement TEXT,
  session_id TEXT
);
CREATE TABLE IF NOT EXISTS eval_items (
  id TEXT PRIMARY KEY,
  question TEXT NOT NULL,
  input_json TEXT NOT NULL,
  source TEXT NOT NULL UNIQUE,
  model_decision TEXT,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS eval_items_question ON eval_items(question, created_at);
CREATE TABLE IF NOT EXISTS labels (
  item_id TEXT NOT NULL,
  source TEXT NOT NULL,
  label TEXT NOT NULL,
  labeled_at TEXT NOT NULL,
  PRIMARY KEY (item_id, source)
);
CREATE TABLE IF NOT EXISTS prompt_sort_axes (
  decision_id TEXT NOT NULL,
  axis TEXT NOT NULL,
  value TEXT NOT NULL,
  heuristic TEXT NOT NULL,
  source TEXT NOT NULL,
  status TEXT NOT NULL,
  probability REAL,
  confidence REAL,
  PRIMARY KEY (decision_id, axis)
);
CREATE INDEX IF NOT EXISTS prompt_sort_axes_axis ON prompt_sort_axes(axis, status);
CREATE TABLE IF NOT EXISTS prompt_sort_runs (
  decision_id TEXT PRIMARY KEY,
  mode TEXT NOT NULL,
  reason TEXT NOT NULL,
  would_fire TEXT NOT NULL DEFAULT '[]',
  fired TEXT NOT NULL DEFAULT '[]',
  suppressed TEXT NOT NULL DEFAULT '[]'
);
`;

export interface BriefRow {
  toolUseId: string;
  agentId: string | null;
  sessionId: string;
  promptId: string;
  dispatchName: string | null;
  subagentType: string;
  goal: string;
  acceptanceCriteria: string;
  proofCommand: string;
  savedAt: string;
}

interface BriefDbRow {
  tool_use_id: string;
  agent_id: string | null;
  session_id: string;
  prompt_id: string;
  dispatch_name: string | null;
  subagent_type: string;
  goal: string;
  acceptance_criteria: string;
  proof_command: string;
  saved_at: string;
}

function rowToBrief(row: BriefDbRow): BriefRow {
  return {
    toolUseId: row.tool_use_id, agentId: row.agent_id, sessionId: row.session_id, promptId: row.prompt_id,
    dispatchName: row.dispatch_name, subagentType: row.subagent_type, goal: row.goal,
    acceptanceCriteria: row.acceptance_criteria, proofCommand: row.proof_command, savedAt: row.saved_at,
  };
}

export function saveBrief(db: Db, row: Omit<BriefRow, "agentId">): void {
  db.prepare(
    `INSERT INTO briefs (tool_use_id, agent_id, session_id, prompt_id, dispatch_name, subagent_type, goal, acceptance_criteria, proof_command, saved_at)
     VALUES (@toolUseId, NULL, @sessionId, @promptId, @dispatchName, @subagentType, @goal, @acceptanceCriteria, @proofCommand, @savedAt)`,
  ).run(row);
}

export function mapToolUseIdToAgentId(db: Db, toolUseId: string, agentId: string): void {
  db.prepare("UPDATE briefs SET agent_id = ? WHERE tool_use_id = ?").run(agentId, toolUseId);
}

export function getBriefByToolUseId(db: Db, toolUseId: string): BriefRow | undefined {
  const row = db.prepare("SELECT * FROM briefs WHERE tool_use_id = ?").get(toolUseId) as BriefDbRow | undefined;
  return row === undefined ? undefined : rowToBrief(row);
}

export function getBriefByAgentId(db: Db, agentId: string): BriefRow | undefined {
  const row = db.prepare("SELECT * FROM briefs WHERE agent_id = ?").get(agentId) as BriefDbRow | undefined;
  return row === undefined ? undefined : rowToBrief(row);
}

export function findUnmappedBriefByTeammateName(db: Db, name: string): BriefRow | undefined {
  const row = db
    .prepare("SELECT * FROM briefs WHERE dispatch_name = ? AND agent_id IS NULL ORDER BY saved_at ASC LIMIT 1")
    .get(name) as BriefDbRow | undefined;
  return row === undefined ? undefined : rowToBrief(row);
}

export function findUnmappedBriefBySubagentDispatch(db: Db, sessionId: string, promptId: string, subagentType: string): BriefRow | undefined {
  const row = db
    .prepare("SELECT * FROM briefs WHERE session_id = ? AND prompt_id = ? AND subagent_type = ? AND agent_id IS NULL ORDER BY saved_at ASC LIMIT 1")
    .get(sessionId, promptId, subagentType) as BriefDbRow | undefined;
  return row === undefined ? undefined : rowToBrief(row);
}

export interface SkippedProvider {
  provider: string;
  reason: "unavailable" | "failed" | "timeout" | "below_threshold";
}

export type DecisionOutcome = "decided" | "escalated" | "failed";

export interface DecisionRow {
  id: string;
  ts: string;
  question: string;
  content_class: string;
  provider: string;
  decision: string | null;
  confidence: number;
  reason_code: string;
  latency_ms: number;
  input_digest: string;
  undone_at: string | null;
  chain_position: number;
  skipped: SkippedProvider[];
  outcome: DecisionOutcome;
}

export interface FailureRow {
  ts: string;
  question: string;
  provider: string;
  reason_code: string;
}

export function openDb(path: string): Db {
  const db = new Database(path);
  db.pragma("journal_mode = WAL");
  db.pragma("busy_timeout = 2000");
  db.exec(MIGRATIONS);
  return db;
}

export function recordDecision(db: Db, row: DecisionRow): void {
  db.prepare(
    `INSERT INTO decisions (id, ts, question, content_class, provider, decision, confidence, reason_code, latency_ms, input_digest, undone_at, chain_position, skipped, outcome)
     VALUES (@id, @ts, @question, @content_class, @provider, @decision, @confidence, @reason_code, @latency_ms, @input_digest, @undone_at, @chain_position, @skipped, @outcome)`,
  ).run({ ...row, skipped: JSON.stringify(row.skipped) });
}

export function recordFailure(db: Db, row: FailureRow): void {
  db.prepare(`INSERT INTO failures (ts, question, provider, reason_code) VALUES (@ts, @question, @provider, @reason_code)`).run(row);
}

export function getDecision(db: Db, id: string): DecisionRow | undefined {
  const row = db.prepare("SELECT * FROM decisions WHERE id = ?").get(id) as (Omit<DecisionRow, "skipped"> & { skipped: string }) | undefined;
  if (row === undefined) return undefined;
  return { ...row, skipped: JSON.parse(row.skipped) as SkippedProvider[] };
}

export function recordUndo(db: Db, id: string, at: string): void {
  db.prepare("UPDATE decisions SET undone_at = ? WHERE id = ?").run(at, id);
}

export type Agreement = "agreed" | "overrode" | "undecided";

export interface DecisionDetailsRow {
  id: string;
  input_json: string;
  probabilities: Record<string, number> | null;
  rules_opinion: string | null;
  agreement: Agreement | null;
  session_id?: string | null;
}

export function recordDecisionDetails(db: Db, row: DecisionDetailsRow): void {
  db.prepare(
    `INSERT OR REPLACE INTO decision_details (id, input_json, probabilities, rules_opinion, agreement, session_id)
     VALUES (@id, @input_json, @probabilities, @rules_opinion, @agreement, @session_id)`,
  ).run({ ...row, session_id: row.session_id ?? null, probabilities: row.probabilities === null ? null : JSON.stringify(row.probabilities) });
}

export function getDecisionDetails(db: Db, id: string): DecisionDetailsRow | undefined {
  const row = db.prepare("SELECT * FROM decision_details WHERE id = ?").get(id) as (Omit<DecisionDetailsRow, "probabilities"> & { probabilities: string | null }) | undefined;
  if (row === undefined) return undefined;
  return { ...row, probabilities: row.probabilities === null ? null : (JSON.parse(row.probabilities) as Record<string, number>) };
}

export function pruneDecisionDetails(db: Db, beforeIso: string): number {
  return db.prepare("DELETE FROM decision_details WHERE id IN (SELECT id FROM decisions WHERE ts < ?)").run(beforeIso).changes;
}

export interface EvalItemRow {
  id: string;
  question: string;
  input_json: string;
  source: string;
  model_decision: string | null;
  created_at: string;
}

export function upsertEvalItem(db: Db, row: EvalItemRow): boolean {
  return db.prepare(
    `INSERT OR IGNORE INTO eval_items (id, question, input_json, source, model_decision, created_at)
     VALUES (@id, @question, @input_json, @source, @model_decision, @created_at)`,
  ).run(row).changes === 1;
}

export function getEvalItem(db: Db, id: string): EvalItemRow | undefined {
  return db.prepare("SELECT * FROM eval_items WHERE id = ?").get(id) as EvalItemRow | undefined;
}

export type LabelSource = "outcome" | "adjudicator" | "override";
const PRECEDENCE: Record<LabelSource, number> = { override: 0, outcome: 1, adjudicator: 2 };

export function recordLabel(db: Db, itemId: string, label: string, at: string, source: LabelSource = "override"): void {
  db.prepare("INSERT OR REPLACE INTO labels (item_id, source, label, labeled_at) VALUES (?, ?, ?, ?)").run(itemId, source, label, at);
}

// The WHERE clause is built in one place so every optional filter extends
// `unlabeledFilter` instead of duplicating the query.
function unlabeledFilter(question: string | undefined, source: LabelSource | undefined, exclude: ReadonlySet<string> | undefined): { join: string; where: string; params: string[] } {
  const params: string[] = [];
  let join = "LEFT JOIN labels l ON l.item_id = e.id";
  if (source !== undefined) {
    join += " AND l.source = ?";
    params.push(source);
  }
  const clauses = ["l.item_id IS NULL"];
  if (question !== undefined) {
    clauses.push("e.question = ?");
    params.push(question);
  }
  if (exclude !== undefined && exclude.size > 0) {
    clauses.push(`e.id NOT IN (${[...exclude].map(() => "?").join(", ")})`);
    params.push(...exclude);
  }
  return { join, where: clauses.join(" AND "), params };
}

export function nextUnlabeled(db: Db, question?: string, source?: LabelSource, exclude?: ReadonlySet<string>): EvalItemRow | undefined {
  const { join, where, params } = unlabeledFilter(question, source, exclude);
  return db.prepare(`SELECT e.* FROM eval_items e ${join} WHERE ${where} ORDER BY e.created_at ASC, e.id ASC LIMIT 1`).get(...params) as EvalItemRow | undefined;
}

export function labeledItems(db: Db, question: string, source: LabelSource | "any" = "any"): Array<EvalItemRow & { label: string; label_source: LabelSource }> {
  const rows = db.prepare(
    `SELECT e.*, l.label, l.source AS label_source FROM eval_items e JOIN labels l ON l.item_id = e.id
     WHERE e.question = ? AND l.label != 'skip' ${source === "any" ? "" : "AND l.source = ?"} ORDER BY e.created_at ASC, e.id ASC`,
  ).all(...(source === "any" ? [question] : [question, source])) as Array<EvalItemRow & { label: string; label_source: LabelSource }>;
  const best = new Map<string, (typeof rows)[number]>();
  for (const r of rows) {
    const cur = best.get(r.id);
    if (cur === undefined || PRECEDENCE[r.label_source] < PRECEDENCE[cur.label_source]) best.set(r.id, r);
  }
  return [...best.values()];
}

export function labelAgreement(db: Db, question: string): { shared: number; agreed: number } {
  const row = db.prepare(
    `SELECT COUNT(*) AS shared, SUM(CASE WHEN o.label = a.label THEN 1 ELSE 0 END) AS agreed
     FROM eval_items e JOIN labels o ON o.item_id = e.id AND o.source = 'outcome' AND o.label != 'skip'
     JOIN labels a ON a.item_id = e.id AND a.source = 'adjudicator' AND a.label != 'skip' WHERE e.question = ?`,
  ).get(question) as { shared: number; agreed: number | null };
  return { shared: row.shared, agreed: row.agreed ?? 0 };
}

// Items carrying both an outcome and an adjudicator label (neither skipped).
export function sharedLabels(db: Db, question: string): Array<{ itemId: string; outcome: string; adjudicator: string }> {
  return db.prepare(
    `SELECT e.id AS itemId, o.label AS outcome, a.label AS adjudicator
     FROM eval_items e JOIN labels o ON o.item_id = e.id AND o.source = 'outcome' AND o.label != 'skip'
     JOIN labels a ON a.item_id = e.id AND a.source = 'adjudicator' AND a.label != 'skip' WHERE e.question = ? ORDER BY e.created_at ASC, e.id ASC`,
  ).all(question) as Array<{ itemId: string; outcome: string; adjudicator: string }>;
}

export function labelCounts(db: Db): Array<{ question: string; items: number; labeled: number; skipped: number }> {
  return db.prepare(
    `SELECT e.question AS question, COUNT(*) AS items,
            SUM(CASE WHEN EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip') THEN 1 ELSE 0 END) AS labeled,
            SUM(CASE WHEN NOT EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip')
                      AND EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label = 'skip') THEN 1 ELSE 0 END) AS skipped
     FROM eval_items e GROUP BY e.question ORDER BY e.question`,
  ).all() as Array<{ question: string; items: number; labeled: number; skipped: number }>;
}
