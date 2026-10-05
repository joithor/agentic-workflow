// judge/tests/prompt-sort/eval-run.test.ts
import { describe, expect, it } from "vitest";

import { openDb, recordLabel, upsertEvalItem } from "../../src/db.js";
import { renderSortEval, runSortEval } from "../../src/prompt-sort/eval-run.js";
import { deps, jevBody, jevFetch, noul } from "./fixtures.js";

const T = "2026-10-01T00:00:00.000Z";
const BUGGY = "Fix the login button crash on the settings page"; // heuristic: bug yes
const PLAIN = "Update the shift export script to use the new column"; // heuristic: bug no

function seed(db: ReturnType<typeof openDb>, id: string, prompt: string, axis: string, label: string, source: "outcome" | "adjudicator") {
  upsertEvalItem(db, { id: `${id}-${axis}`, question: `prompt-sort:${axis}`, input_json: JSON.stringify({ prompt }), source: `decision:${id}:${axis}`, model_decision: null, created_at: T });
  recordLabel(db, `${id}-${axis}`, label, T, source);
}

describe("runSortEval", () => {
  it("scores heuristic and blend per axis and label source, with precision of the scaffold class", async () => {
    const db = openDb(":memory:");
    // Adjudicator says: BUGGY is a bug (heuristic right), PLAIN is a bug (heuristic wrong; the judge, below, is right).
    seed(db, "p1", BUGGY, "is_bug_report", "yes", "adjudicator");
    seed(db, "p2", PLAIN, "is_bug_report", "yes", "adjudicator");
    // Outcome (recall-only): the session invoked a bug skill for PLAIN only.
    seed(db, "p2", PLAIN, "is_bug_report", "yes", "outcome");
    const fetch = jevFetch(jevBody({ is_bug_report: noul(0.9) })); // the judge calls every prompt a bug
    const r = await runSortEval(db, { sortDeps: { jev: deps(fetch), budgetMs: 1000 }, now: () => new Date("2026-10-04T00:00:00.000Z") });
    const adj = r.summaries.find((s) => s.axis === "is_bug_report" && s.source === "adjudicator");
    expect(adj).toMatchObject({ n: 2, heuristic: { accuracy: 0.5, ppv: 1 }, blend: { accuracy: 1, ppv: 1 } });
    const out = r.summaries.find((s) => s.axis === "is_bug_report" && s.source === "outcome");
    expect(out).toMatchObject({ n: 1, heuristic: { accuracy: 0 }, blend: { accuracy: 1 } });
    expect(r.agreement.find((a) => a.axis === "is_bug_report")).toEqual({ axis: "is_bug_report", shared: 1, agreed: 1 });
    expect(r.ranAt).toBe("2026-10-04T00:00:00.000Z");
    // One Jev request per distinct prompt, shared by every axis and source.
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it("collapses complexity and ambiguity before scoring (RF-6) and skips axes with no labels", async () => {
    const db = openDb(":memory:");
    seed(db, "p1", PLAIN, "complexity", "not-large", "outcome");
    seed(db, "p1", PLAIN, "complexity", "small", "adjudicator");
    const r = await runSortEval(db, { sortDeps: { jev: deps(jevFetch(jevBody())), budgetMs: 1000 }, now: () => new Date("2026-10-04T00:00:00.000Z") });
    expect(r.summaries.filter((s) => s.axis === "complexity").map((s) => [s.source, s.blend.accuracy])).toEqual([["adjudicator", 1], ["outcome", 1]]);
    expect(r.summaries.some((s) => s.axis === "touches_ui")).toBe(false);
    expect(r.agreement.find((a) => a.axis === "complexity")).toEqual({ axis: "complexity", shared: 1, agreed: 1 });
  });

  it("reports a null ppv when a policy never predicts the scaffold class", async () => {
    const db = openDb(":memory:");
    seed(db, "p1", PLAIN, "touches_ui", "no", "adjudicator");
    const r = await runSortEval(db, { sortDeps: { jev: deps(jevFetch(jevBody({ touches_ui: noul(0.05) }))), budgetMs: 1000 }, now: () => new Date("2026-10-04T00:00:00.000Z") });
    expect(r.summaries[0]).toMatchObject({ heuristic: { accuracy: 1, ppv: null }, blend: { accuracy: 1, ppv: null } });
  });
});

describe("renderSortEval", () => {
  it("renders the per-source table and the agreement table, with n/a for a null precision", () => {
    const md = renderSortEval({
      ranAt: "2026-10-04T00:00:00.000Z",
      summaries: [
        { axis: "touches_ui", source: "adjudicator", n: 7, heuristic: { accuracy: 0.5, ppv: null }, blend: { accuracy: 0.75, ppv: 0.8 } },
        { axis: "touches_ui", source: "outcome", n: 3, heuristic: { accuracy: 0.5, ppv: 0.5 }, blend: { accuracy: 1, ppv: null } },
      ],
      agreement: [{ axis: "touches_ui", shared: 4, agreed: 3 }],
    });
    expect(md).toContain("| Axis | Source | n | Heuristic acc | Blend acc | Blend precision |");
    expect(md).toContain("| touches_ui | adjudicator | 7 | 50.0% | 75.0% | 80.0% |");
    expect(md).toContain("| touches_ui | outcome | 3 | 50.0% | 100.0% | n/a |");
    expect(md).toContain("| touches_ui | 4 | 3 |");
  });
});

describe("runSortEval ppv class (C3)", () => {
  it("measures verification_defined precision on the class that triggers brief (no)", async () => {
    const db = openDb(":memory:");
    seed(db, "p1", PLAIN, "verification_defined", "no", "adjudicator");
    const r = await runSortEval(db, { sortDeps: { jev: deps(jevFetch(jevBody({ verification_defined: noul(0.05) }))), budgetMs: 1000 }, now: () => new Date("2026-10-04T00:00:00.000Z") });
    expect(r.summaries[0]).toMatchObject({ axis: "verification_defined", blend: { accuracy: 1, ppv: 1 } });
  });
});
