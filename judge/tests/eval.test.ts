import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { openDb, recordLabel, upsertEvalItem, type Db } from "../src/db.js";
import { SOURCE_WARNING, VARIANTS, makeRecorder, readReplay, renderEvalReport, runEval, scoreEval, writeEvalReport, type EvalResult } from "../src/eval.js";
import { fakeProvider } from "./helpers.js";

const decided = (decision: string, confidence: number) => ({ status: "decided" as const, decision, confidence, reason_code: "x" });
const INPUT = '{"text":"step 2 done","senderKind":"teammate"}';
const tmp = () => fs.mkdtempSync(path.join(os.tmpdir(), "judge-eval-"));

describe("scoreEval", () => {
  it("computes accuracy, decisive rate, accuracy-on-decisive, p50 latency and the sweep", () => {
    const results: EvalResult[] = [
      { itemId: "1", label: "send", result: decided("send", 0.95), latencyMs: 100 },
      { itemId: "2", label: "send", result: decided("batch", 0.65), latencyMs: 300 },
      { itemId: "3", label: "drop", result: decided("drop", 0.55), latencyMs: 200 },
      { itemId: "4", label: "send", result: { status: "unavailable", reason_code: "timeout" }, latencyMs: 900 },
    ];
    const r = scoreEval("wake-gate", "jev", "as-is", 0.7, results);
    expect(r).toMatchObject({ n: 4, decided: 3, accuracy: 0.5, decisiveRate: 0.25, accuracyDecisive: 1, p50LatencyMs: 200, labelSource: "any" });
    expect(r.sweep.find((s) => s.threshold === 0.6)).toEqual({ threshold: 0.6, coverage: 0.5, accuracyCovered: 0.5 });
    expect(r.sweep.find((s) => s.threshold === 0.9)).toEqual({ threshold: 0.9, coverage: 0.25, accuracyCovered: 1 });
    const md = renderEvalReport(r);
    expect(md).toContain("| 0.9 | 25.0% | 100.0% |");
    expect(md).toContain("Label source: any");
    expect(md).not.toContain("WARNING");
  });

  it("reports zeros, not NaN, on an empty set", () => {
    expect(scoreEval("q", "jev", "as-is", 0.7, [])).toMatchObject({ n: 0, accuracy: 0, decisiveRate: 0, accuracyDecisive: 0, p50LatencyMs: 0 });
  });
});

describe("runEval", () => {
  function labeled() {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "i1", question: "wake-gate", input_json: INPUT, source: "decision:a", model_decision: "send", created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "i1", "batch", "2026-10-02T00:00:00.000Z");
    return db;
  }

  it("calls the provider once per labeled item, records each result line, and never writes decisions", async () => {
    const db = labeled();
    const lines: string[] = [];
    const out = await runEval(db, { question: "wake-gate", provider: fakeProvider("jev", ["message-meta"], decided("batch", 0.9)), variant: "as-is", record: (l) => lines.push(l) });
    expect(out.exitCode).toBe(0);
    expect(out.report).toMatchObject({ n: 1, accuracy: 1, labelSource: "any" });
    expect(out.report?.perSource).toBeUndefined();
    expect(JSON.parse(lines[0] as string)).toMatchObject({ itemId: "i1", label: "batch", result: { decision: "batch" } });
    expect((db.prepare("SELECT COUNT(*) n FROM decisions").get() as { n: number }).n).toBe(0);
  });

  it("works without a recorder and counts a schema-invalid stored input as undecided", async () => {
    const db = labeled();
    upsertEvalItem(db, { id: "i2", question: "wake-gate", input_json: '{"nope":1}', source: "decision:b", model_decision: null, created_at: "2026-10-01T00:00:01.000Z" });
    recordLabel(db, "i2", "send", "2026-10-02T00:00:00.000Z");
    const out = await runEval(db, { question: "wake-gate", provider: fakeProvider("jev", ["message-meta"], decided("batch", 0.9)), variant: "as-is" });
    expect(out.report).toMatchObject({ n: 2, decided: 1, accuracy: 0.5 });
  });

  it("replays recorded results without calling the provider", async () => {
    const db = labeled();
    const provider = fakeProvider("jev", ["message-meta"], () => { throw new Error("must not be called"); });
    const out = await runEval(db, { question: "wake-gate", provider, variant: "as-is", replay: [{ itemId: "i1", label: "batch", result: decided("send", 0.9), latencyMs: 5 }] });
    expect(out.report).toMatchObject({ n: 1, accuracy: 0 });
  });

  it("applies a registered variant to the stored input", async () => {
    const db = labeled();
    VARIANTS["wake-gate"] = { upper: (i) => ({ ...(i as object), text: "UPPER" }) };
    let seen = "";
    const p = fakeProvider("jev", ["message-meta"], (q) => { seen = q.prompt; return decided("batch", 0.9); });
    const out = await runEval(db, { question: "wake-gate", provider: p, variant: "upper" });
    delete VARIANTS["wake-gate"];
    expect(out.exitCode).toBe(0);
    expect(seen).toContain("UPPER");
  });

  it("errors on an unknown question, an unknown variant, or no labeled items", async () => {
    const db = openDb(":memory:");
    const p = fakeProvider("jev", ["message-meta"], decided("send", 1));
    expect((await runEval(db, { question: "nope", provider: p, variant: "as-is" })).exitCode).toBe(1);
    expect((await runEval(db, { question: "wake-gate", provider: p, variant: "nope" })).exitCode).toBe(1);
    expect(await runEval(db, { question: "wake-gate", provider: p, variant: "as-is" })).toMatchObject({ exitCode: 1, stderr: "no labeled items for wake-gate (run: judge label import, judge label outcomes, judge adjudicate wake-gate)" });
  });
});

describe("per-source reporting", () => {
  function twoSources(adjudicatorLabels: string[]): Db {
    const db = openDb(":memory:");
    adjudicatorLabels.forEach((adj, i) => {
      const id = `i${i}`;
      upsertEvalItem(db, { id, question: "wake-gate", input_json: INPUT, source: `decision:${id}`, model_decision: "send", created_at: `2026-10-01T00:00:0${i}.000Z` });
      recordLabel(db, id, "send", "2026-10-02T00:00:00.000Z", "outcome");
      recordLabel(db, id, adj, "2026-10-02T00:00:00.000Z", "adjudicator");
    });
    return db;
  }
  const send = () => fakeProvider("jev", ["message-meta"], decided("send", 0.9));

  it("scores against each source and reports agreement without a warning when sources agree", async () => {
    const out = await runEval(twoSources(["send", "send", "send", "send"]), { question: "wake-gate", provider: send(), variant: "as-is" });
    expect(out.report?.perSource).toMatchObject({ agreement: { shared: 4, agreed: 4, rate: 1 }, outcome: { accuracy: 1, labelSource: "outcome" }, adjudicator: { accuracy: 1, labelSource: "adjudicator" } });
    expect(out.report?.sourceWarning).toBeUndefined();
    const md = renderEvalReport(out.report as NonNullable<typeof out.report>);
    expect(md).toContain("Outcome vs adjudicator agreement: 4/4 (100.0%)");
    expect(md).toContain("- outcome: n=4");
    expect(md).not.toContain("WARNING");
  });

  it("flags >30% disagreement and scores each source separately", async () => {
    const out = await runEval(twoSources(["send", "batch", "batch", "send"]), { question: "wake-gate", provider: send(), variant: "as-is" });
    expect(out.report).toMatchObject({ labelSource: "any", sourceWarning: SOURCE_WARNING });
    expect(out.report?.perSource).toMatchObject({ agreement: { shared: 4, agreed: 2, rate: 0.5 }, outcome: { accuracy: 1 }, adjudicator: { accuracy: 0.5 } });
    expect(JSON.parse(out.stdout).sourceWarning).toBe(SOURCE_WARNING);
    expect(renderEvalReport(out.report as NonNullable<typeof out.report>)).toContain(`WARNING: ${SOURCE_WARNING}`);
  });

  it("does not flag exactly 30% disagreement", async () => {
    const labels = ["send", "send", "send", "send", "send", "send", "send", "batch", "batch", "batch"];
    const out = await runEval(twoSources(labels), { question: "wake-gate", provider: send(), variant: "as-is" });
    expect(out.report?.perSource?.agreement.rate).toBeCloseTo(0.7);
    expect(out.report?.sourceWarning).toBeUndefined();
  });

  it("replay scores against the db labels of the current --labels source, counting only items labeled in it", async () => {
    const db = twoSources(["batch", "batch"]);
    upsertEvalItem(db, { id: "solo", question: "wake-gate", input_json: INPUT, source: "decision:solo", model_decision: "send", created_at: "2026-10-01T00:00:09.000Z" });
    recordLabel(db, "solo", "send", "2026-10-02T00:00:00.000Z", "outcome");
    // Rows were recorded against stale labels ("send"); adjudicator says "batch".
    const replay: EvalResult[] = ["i0", "i1", "solo"].map((itemId) => ({ itemId, label: "send", result: decided("send", 0.9), latencyMs: 1 }));
    const out = await runEval(db, { question: "wake-gate", provider: send(), variant: "as-is", labels: "adjudicator", replay });
    expect(out.report).toMatchObject({ n: 2, accuracy: 0, labelSource: "adjudicator" });
  });

  it("replay counts a duplicated itemId once, last row winning", async () => {
    const db = twoSources(["batch"]);
    const replay: EvalResult[] = [
      { itemId: "i0", label: "batch", result: decided("send", 0.9), latencyMs: 1 },
      { itemId: "i0", label: "batch", result: decided("batch", 0.9), latencyMs: 1 },
    ];
    const out = await runEval(db, { question: "wake-gate", provider: send(), variant: "as-is", labels: "adjudicator", replay });
    expect(out.report).toMatchObject({ n: 1, accuracy: 1 });
  });

  it("records the requested label source and omits per-source when replay covers no shared item", async () => {
    const out = await runEval(twoSources(["send"]), { question: "wake-gate", provider: send(), variant: "as-is", labels: "outcome", replay: [{ itemId: "other", label: "send", result: decided("send", 0.9), latencyMs: 1 }] });
    expect(out.report?.labelSource).toBe("outcome");
    expect(out.report?.perSource).toBeUndefined();
  });
});

describe("file helpers", () => {
  it("creates missing directories for --record, replays the file, and writes the report", () => {
    const root = tmp();
    const rec = path.join(root, "a", "b", "run.jsonl");
    const record = makeRecorder(rec);
    const row: EvalResult = { itemId: "i1", label: "send", result: decided("send", 0.9), latencyMs: 3 };
    record(JSON.stringify(row));
    record(JSON.stringify(row));
    expect(readReplay(rec)).toEqual([row, row]);

    const dir = path.join(root, "x", "evals");
    const file = writeEvalReport(dir, scoreEval("wake-gate", "jev", "as-is", 0.7, [row]), 123);
    expect(file).toBe(path.join(dir, "wake-gate-jev-as-is-123.md"));
    expect(fs.readFileSync(file, "utf8")).toContain("# judge eval: wake-gate / jev / as-is");
  });
});

describe("prompt-sort axis evals (RF-6)", () => {
  function sortDb() {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "c1", question: "prompt-sort:complexity", input_json: '{"prompt":"rewrite the whole thing"}', source: "decision:a:complexity", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "c1", "large", "2026-10-02T00:00:00.000Z", "outcome");
    return db;
  }

  it("scores a fine prediction against a coarse outcome label in the collapsed space", async () => {
    const out = await runEval(sortDb(), { question: "prompt-sort:complexity", provider: fakeProvider("jev", ["brief"], decided("large", 1)), variant: "blend", labels: "outcome" });
    expect(out.report).toMatchObject({ n: 1, accuracy: 1 });
    const wrong = await runEval(sortDb(), { question: "prompt-sort:complexity", provider: fakeProvider("jev", ["brief"], decided("substantial", 1)), variant: "blend", labels: "outcome" });
    expect(wrong.report).toMatchObject({ accuracy: 0 });
  });

  it("passes the variant as the input's mode and rejects unknown variants", async () => {
    let seen: unknown;
    const provider = fakeProvider("jev", ["brief"], (_q, input) => { seen = input; return decided("large", 1); });
    await runEval(sortDb(), { question: "prompt-sort:complexity", provider, variant: "heuristic", labels: "outcome" });
    expect(seen).toMatchObject({ mode: "heuristic" });
    expect((await runEval(sortDb(), { question: "prompt-sort:complexity", provider, variant: "nope" })).exitCode).toBe(1);
    expect(VARIANTS["prompt-sort:ambiguity"]?.["judge-only"]?.(null)).toEqual({ mode: "judge-only" });
  });

  it("collapses on replay too", async () => {
    const out = await runEval(sortDb(), { question: "prompt-sort:complexity", provider: fakeProvider("jev", ["brief"], decided("x", 1)), variant: "blend", labels: "outcome", replay: [{ itemId: "c1", label: "large", result: decided("large", 1), latencyMs: 1 }] });
    expect(out.report).toMatchObject({ accuracy: 1 });
  });

  it("C1: the collapse survives withSources (per-source scores and agreement use collapsed labels)", async () => {
    const db = openDb(":memory:");
    for (const [id, outcome, adjudicator] of [["c1", "not-large", "small"], ["c2", "large", "large"]] as const) {
      upsertEvalItem(db, { id, question: "prompt-sort:complexity", input_json: '{"prompt":"x y z"}', source: `decision:${id}:complexity`, model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
      recordLabel(db, id, outcome, "2026-10-02T00:00:00.000Z", "outcome");
      recordLabel(db, id, adjudicator, "2026-10-02T00:00:00.000Z", "adjudicator");
    }
    // Fine prediction "trivial" collapses to not-large: right for c1, wrong for c2.
    const out = await runEval(db, { question: "prompt-sort:complexity", provider: fakeProvider("jev", ["brief"], decided("trivial", 1)), variant: "blend" });
    const per = out.report?.perSource;
    expect(per?.agreement).toEqual({ shared: 2, agreed: 2, rate: 1 });
    expect(per?.outcome).toMatchObject({ n: 2, accuracy: 0.5 });
    expect(per?.adjudicator).toMatchObject({ n: 2, accuracy: 0.5 });
    expect(out.report?.sourceWarning).toBeUndefined();
  });

  it("leaves unavailable results uncollapsed", async () => {
    const out = await runEval(sortDb(), { question: "prompt-sort:complexity", provider: fakeProvider("jev", ["brief"], { status: "unavailable", reason_code: "jev-unavailable" }), variant: "blend", labels: "outcome" });
    expect(out.report).toMatchObject({ n: 1, decided: 0, accuracy: 0 });
  });
});

describe("context variants", () => {
  const rc = { brief: "b", expected: "e", actual: "a", rootCause: "rc", checkKind: "test", checkSummary: "s", beforePassed: false, afterPassed: true, diffStat: "1 file", diff: "+x" };
  it("strips resolution-check context progressively", () => {
    const v = VARIANTS["resolution-check"] as NonNullable<(typeof VARIANTS)[string]>;
    expect(v["brief-only"]?.(rc)).toEqual({ ...rc, rootCause: "", diffStat: "", diff: undefined });
    expect(v["brief+cause"]?.(rc)).toEqual({ ...rc, diffStat: "", diff: undefined });
    expect(v.stat?.(rc)).toEqual({ ...rc, diff: undefined });
    expect(v.full?.(rc)).toEqual(rc);
    expect(v.stat?.(null)).toEqual({ diff: undefined });
  });

  it("strips turn-progress context progressively", () => {
    const tp = { problem: "p", acceptanceCriteria: "ac", turnDiff: "+x", priorDiffStat: "1 file", signals: "tests: failed" };
    const v = VARIANTS["turn-progress"] as NonNullable<(typeof VARIANTS)[string]>;
    expect(v["diff-only"]?.(tp)).toEqual({ ...tp, problem: "(not given)", acceptanceCriteria: "" });
    expect(v["problem+diff"]?.(tp)).toEqual({ ...tp, acceptanceCriteria: "", priorDiffStat: "", signals: "" });
    expect(v.full?.(tp)).toEqual(tp);
  });

  const rcDb = () => {
    const db = openDb(":memory:");
    const items: Array<[string, Record<string, unknown>]> = [["d1", rc], ["n1", (({ diff: _d, ...r }) => r)(rc)]];
    items.forEach(([id, input], i) => {
      upsertEvalItem(db, { id, question: "resolution-check", input_json: JSON.stringify(input), source: `decision:${id}`, model_decision: null, created_at: `2026-10-01T00:00:0${i}.000Z` });
      recordLabel(db, id, "resolved", "2026-10-02T00:00:00.000Z", "outcome");
    });
    return db;
  };

  it("B5: stat and full score the same population, only items with a stored diff", async () => {
    const seen: unknown[] = [];
    const provider = fakeProvider("jev", ["brief", "diff"], (_q, input) => { seen.push(input); return decided("resolved", 1); });
    for (const variant of ["stat", "full"]) {
      const out = await runEval(rcDb(), { question: "resolution-check", provider, variant, labels: "outcome" });
      expect(out.report).toMatchObject({ n: 1, decided: 1, accuracy: 1 });
    }
    expect(seen).toHaveLength(2);
    // Variants that do not use the diff still score every item.
    const brief = await runEval(rcDb(), { question: "resolution-check", provider, variant: "brief-only", labels: "outcome" });
    expect(brief.report).toMatchObject({ n: 2 });
  });
});

describe("--collapse", () => {
  const db = () => {
    const d = openDb(":memory:");
    upsertEvalItem(d, { id: "i1", question: "wake-gate", input_json: INPUT, source: "decision:a", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(d, "i1", "off-target", "2026-10-02T00:00:00.000Z", "outcome");
    recordLabel(d, "i1", "stalled", "2026-10-02T00:00:00.000Z", "adjudicator");
    return d;
  };
  const prov = () => fakeProvider("jev", ["message-meta"], decided("stalled", 1));

  it("folds every label but the positive one into stalled, in results, per-source scores and agreement", async () => {
    const plain = await runEval(db(), { question: "wake-gate", provider: prov(), variant: "as-is" });
    expect(plain.report?.perSource?.agreement).toEqual({ shared: 1, agreed: 0, rate: 0 });
    expect(plain.report?.perSource?.outcome).toMatchObject({ accuracy: 0 });
    const out = await runEval(db(), { question: "wake-gate", provider: prov(), variant: "as-is", collapse: "send" });
    expect(out.report).toMatchObject({ accuracy: 1 });
    expect(out.report?.perSource?.agreement).toEqual({ shared: 1, agreed: 1, rate: 1 });
    expect(out.report?.perSource?.outcome).toMatchObject({ accuracy: 1 });
    expect(out.report?.perSource?.adjudicator).toMatchObject({ accuracy: 1 });
  });

  it("rejects a --collapse label the question does not output, before scoring", async () => {
    const out = await runEval(db(), { question: "wake-gate", provider: prov(), variant: "as-is", collapse: "progressing" });
    expect(out).toEqual({ exitCode: 1, stdout: "", stderr: "--collapse must be one of: send, batch, drop (got progressing)" });
  });

  it("accepts a collapsed axis label for prompt-sort questions", async () => {
    const out = await runEval(db(), { question: "prompt-sort:complexity", provider: prov(), variant: "as-is", collapse: "large" });
    expect(out.stderr ?? "").not.toContain("--collapse");
  });

  it("keeps the positive label and collapses on replay too", async () => {
    const d = openDb(":memory:");
    upsertEvalItem(d, { id: "p1", question: "wake-gate", input_json: INPUT, source: "decision:p", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(d, "p1", "send", "2026-10-02T00:00:00.000Z");
    const replay = [{ itemId: "p1", label: "x", result: decided("send", 1), latencyMs: 1 }];
    expect((await runEval(d, { question: "wake-gate", provider: prov(), variant: "as-is", collapse: "send", replay })).report).toMatchObject({ accuracy: 1 });
    const wrong = [{ itemId: "p1", label: "x", result: decided("drop", 1), latencyMs: 1 }];
    expect((await runEval(d, { question: "wake-gate", provider: prov(), variant: "as-is", collapse: "send", replay: wrong })).report).toMatchObject({ accuracy: 0 });
  });
});
