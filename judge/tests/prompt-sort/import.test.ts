import { describe, expect, it } from "vitest";

import { getDecisionDetails, nextUnlabeled, openDb } from "../../src/db.js";
import { ALL_AXES } from "../../src/prompt-sort/axes.js";
import { blend } from "../../src/prompt-sort/blend.js";
import { heuristicSort } from "../../src/prompt-sort/heuristics.js";
import { importSortItems } from "../../src/prompt-sort/import.js";
import { recordPromptSort } from "../../src/prompt-sort/store.js";
import type { SortOutcome } from "../../src/prompt-sort/sort.js";

const PROMPT = "Fix the login button crash on the settings page";
const outcome = (): SortOutcome => ({ ...blend(heuristicSort(PROMPT), { is_task: { noul: 0.9 } }), mode: "blend", reason: "sorted", failure: null, latencyMs: 1, usage: null, sentPrompt: PROMPT });
const put = (db: ReturnType<typeof openDb>, id: string, ts: string, fired: Array<"brief">) =>
  recordPromptSort(db, { id, ts, sessionId: "s1", rawPrompt: PROMPT, outcome: outcome(), wouldFire: ["brief"], fired, suppressed: [] });

describe("importSortItems", () => {
  it("creates one eval item per axis per decision with the stored redacted prompt, once", () => {
    const db = openDb(":memory:");
    put(db, "d1", "2026-10-02T00:00:00.000Z", []);
    expect(importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" })).toEqual({ imported: 11, contaminated: 0, existing: 0 });
    expect(importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" })).toEqual({ imported: 0, contaminated: 0, existing: 11 });
    const item = nextUnlabeled(db, "prompt-sort:is_task");
    expect(item).toMatchObject({ id: "ps-d1-is_task", source: "decision:d1:is_task", model_decision: null, created_at: "2026-10-02T00:00:00.000Z" });
    expect(JSON.parse(item?.input_json ?? "")).toEqual({ prompt: PROMPT });
    expect(getDecisionDetails(db, "d1")?.session_id).toBe("s1");
    expect(ALL_AXES.every((a) => nextUnlabeled(db, `prompt-sort:${a}`) !== undefined)).toBe(true);
  });

  it("skips decisions where a scaffold fired (their next steps are not independent) and old decisions (RF-5)", () => {
    const db = openDb(":memory:");
    put(db, "fired", "2026-10-02T00:00:00.000Z", ["brief"]);
    put(db, "old", "2026-08-01T00:00:00.000Z", []);
    expect(importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" })).toEqual({ imported: 0, contaminated: 1, existing: 0 });
    expect(nextUnlabeled(db, "prompt-sort:is_task")).toBeUndefined();
  });

  it("never writes decisions", () => {
    const db = openDb(":memory:");
    put(db, "d1", "2026-10-02T00:00:00.000Z", []);
    const before = (db.prepare("SELECT COUNT(*) AS n FROM decisions").get() as { n: number }).n;
    importSortItems(db, { sinceIso: "2026-10-01T00:00:00.000Z" });
    expect((db.prepare("SELECT COUNT(*) AS n FROM decisions").get() as { n: number }).n).toBe(before);
  });
});
