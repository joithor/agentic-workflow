// Label commands: import decided rows as eval items, set an optional manual
// override, and report coverage. Labels themselves come from outcomes.ts and
// adjudicate.ts; nobody is expected to label by hand.
import { QUESTIONS } from "./commands.js";
import { getEvalItem, labelAgreement, labelCounts, recordLabel, upsertEvalItem, type Db } from "./db.js";

type Result = { exitCode: number; stdout: string; stderr?: string };

export function runLabelImport(db: Db, opts: { question?: string; sinceIso: string }, now: () => Date): { exitCode: number; stdout: string } {
  const rows = db.prepare(
    `SELECT d.id, d.question, d.decision, x.input_json FROM decisions d JOIN decision_details x ON x.id = d.id
     WHERE d.outcome = 'decided' AND d.ts >= ? ${opts.question === undefined ? "" : "AND d.question = ?"} ORDER BY d.ts ASC, d.id ASC`,
  ).all(...(opts.question === undefined ? [opts.sinceIso] : [opts.sinceIso, opts.question])) as Array<{ id: string; question: string; decision: string | null; input_json: string }>;
  let imported = 0;
  let skippedUnparsable = 0;
  for (const r of rows) {
    const q = QUESTIONS[r.question];
    if (q === undefined) continue;
    const parsed = parseJson(r.input_json);
    if (!parsed.ok) {
      skippedUnparsable++;
      continue;
    }
    if (!q.inputSchema.safeParse(parsed.value).success) continue;
    const added = upsertEvalItem(db, {
      id: `item-${r.id}`, question: r.question, input_json: r.input_json, source: `decision:${r.id}`,
      model_decision: r.decision, created_at: now().toISOString(),
    });
    if (added) imported++;
  }
  return { exitCode: 0, stdout: JSON.stringify({ imported, skipped_unparsable: skippedUnparsable }) };
}

function parseJson(json: string): { ok: true; value: unknown } | { ok: false } {
  try {
    return { ok: true, value: JSON.parse(json) };
  } catch {
    return { ok: false };
  }
}

export function runLabelSet(db: Db, itemId: string, label: string, now: () => Date): Result {
  const item = getEvalItem(db, itemId);
  if (item === undefined) return { exitCode: 1, stdout: "", stderr: `unknown item: ${itemId}` };
  const q = QUESTIONS[item.question];
  const valid = [...(q === undefined ? [] : (q.outputs as readonly string[])), "skip"];
  if (!valid.includes(label)) return { exitCode: 1, stdout: "", stderr: `label must be one of: ${valid.join(", ")}` };
  recordLabel(db, itemId, label, now().toISOString(), "override");
  return { exitCode: 0, stdout: JSON.stringify({ itemId, label }) };
}

export function runLabelStatus(db: Db): { exitCode: number; stdout: string } {
  const status = labelCounts(db).map((c) => ({ ...c, agreement: labelAgreement(db, c.question) }));
  return { exitCode: 0, stdout: JSON.stringify(status) };
}
