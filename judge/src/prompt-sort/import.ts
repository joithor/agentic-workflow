// judge/src/prompt-sort/import.ts
// Turns recorded prompt-sort decisions into per-axis eval items. Decisions on
// which a scaffold FIRED are excluded: what happens next in such a session is
// partly caused by the scaffold, so it cannot label the sorter (RF-5).
import type { Db } from "../db.js";
import { upsertEvalItem } from "../db.js";
import { ALL_AXES, sortQuestionName } from "./axes.js";

export function importSortItems(db: Db, opts: { sinceIso: string }): { imported: number; contaminated: number; existing: number } {
  const rows = db
    .prepare(
      `SELECT d.id AS id, d.ts AS ts, x.input_json AS input_json, r.fired AS fired
       FROM decisions d
       JOIN decision_details x ON x.id = d.id
       LEFT JOIN prompt_sort_runs r ON r.decision_id = d.id
       WHERE d.question = 'prompt-sort' AND d.outcome = 'decided' AND d.ts >= ?
       ORDER BY d.ts ASC`,
    )
    .all(opts.sinceIso) as Array<{ id: string; ts: string; input_json: string; fired: string | null }>;
  let imported = 0;
  let contaminated = 0;
  let existing = 0;
  for (const row of rows) {
    if (row.fired !== null && row.fired !== "[]") {
      contaminated++;
      continue;
    }
    for (const axis of ALL_AXES) {
      const inserted = upsertEvalItem(db, {
        id: `ps-${row.id}-${axis}`, question: sortQuestionName(axis), input_json: row.input_json,
        source: `decision:${row.id}:${axis}`, model_decision: null, created_at: row.ts,
      });
      if (inserted) imported++;
      else existing++;
    }
  }
  return { imported, contaminated, existing };
}
