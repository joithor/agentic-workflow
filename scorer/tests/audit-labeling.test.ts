import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import type { HumanTurn } from "../src/audit/human-turns.js";
import type { LabelRunner } from "../src/audit/labels.js";
import type { LabelingReport } from "../src/audit/labeling.js";
import { observedWindowDays, renderLabeling, runLabeling } from "../src/audit/labeling.js";

const turn = (i: number): HumanTurn => ({
  project: "p", session: `s${i % 4}`, ts: `2026-10-05T00:${String(i).padStart(2, "0")}:00Z`, index: i, kind: "turn", text: `are you sure about step ${i}`,
  skills: [], guardFiredBefore: false, compactedBefore: false, editsBefore: i % 2 === 0, contextTokens: 0, prevAssistantTail: "done",
});
const idsIn = (prompt: string): string[] => [...prompt.matchAll(/<untrusted id="(t\d+)">/g)].map((m) => m[1]);

describe("runLabeling", () => {
  it("samples, labels, repeats the first k in reverse batch order, and writes labels.jsonl and calibration.json", async () => {
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
    const calls: string[][] = [];
    const runner: LabelRunner = async (prompt) => {
      const ids = idsIn(prompt);
      calls.push(ids);
      return { labels: ids.map((id) => ({ id, labels: ["rigor"] })) };
    };
    const turns = Array.from({ length: 30 }, (_, i) => turn(i));
    const report = await runLabeling(turns, 30, { n: 25, repeat: 5, model: "sonnet", runner, windowDays: 30 }, out);

    expect(calls.map((c) => c.length)).toEqual([20, 5, 5]); // 25 sampled in 2 batches, then 5 repeated in 1
    expect(calls[2]).toEqual(["t4", "t3", "t2", "t1", "t0"]); // second pass runs in reverse order
    expect(report).toMatchObject({ model: "sonnet", requested: 25, sampled: 25, labeled: 25, labelErrors: 0 });
    expect(report.repeat).toMatchObject({ requested: 5, compared: 5 });
    expect(report.repeat.agreement.rigor).toBe(1);
    expect(report.calibration.find((c) => c.pattern === "rigor")).toMatchObject({ tp: 25, fp: 0, fn: 0 });

    const lines = fs.readFileSync(path.join(out, "labels.jsonl"), "utf8").trim().split("\n").map((l) => JSON.parse(l) as { key: string; labels: string[]; model: string; pass: number });
    expect(lines).toHaveLength(30);
    expect(lines.filter((l) => l.pass === 2)).toHaveLength(5);
    expect(lines[0]).toMatchObject({ labels: ["rigor"], model: "sonnet", pass: 1 });
    expect(lines[0].key).toMatch(/^s\d:\d+$/);
    const calibration = fs.readFileSync(path.join(out, "calibration.json"), "utf8");
    expect(JSON.parse(calibration)).toMatchObject({ labeled: 25 });
    expect(calibration).not.toContain("are you sure");
    expect(fs.statSync(path.join(out, "labels.jsonl")).mode & 0o777).toBe(0o600);
  });

  it("tightens an existing labels.jsonl to owner-only", async () => {
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
    fs.writeFileSync(path.join(out, "labels.jsonl"), "", { mode: 0o644 });
    const runner: LabelRunner = async (prompt) => ({ labels: idsIn(prompt).map((id) => ({ id, labels: ["none"] })) });
    await runLabeling([turn(0)], 1, { n: 1, repeat: 0, model: "sonnet", runner, windowDays: 30 }, out);
    expect(fs.statSync(path.join(out, "labels.jsonl")).mode & 0o777).toBe(0o600);
  });

  it("is reproducible: the same corpus yields the same sampled keys", async () => {
    const run = async (): Promise<string[]> => {
      const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
      const runner: LabelRunner = async (prompt) => ({ labels: idsIn(prompt).map((id) => ({ id, labels: ["none"] })) });
      await runLabeling(Array.from({ length: 30 }, (_, i) => turn(i)), 30, { n: 10, repeat: 0, model: "sonnet", runner, windowDays: 30 }, out);
      return fs.readFileSync(path.join(out, "labels.jsonl"), "utf8").trim().split("\n").map((l) => (JSON.parse(l) as { key: string }).key);
    };
    expect(await run()).toEqual(await run());
  });

  it("excludes turns of failed batches from calibration and counts the errors", async () => {
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
    const runner: LabelRunner = async () => {
      throw new Error("down");
    };
    const report = await runLabeling(Array.from({ length: 5 }, (_, i) => turn(i)), 5, { n: 5, repeat: 0, model: "sonnet", runner, windowDays: 30 }, out);
    expect(report).toMatchObject({ sampled: 5, labeled: 0, labelErrors: 1 });
    expect(report.wrongApproach.decision).toBe("no-data");
  });
});

describe("observedWindowDays", () => {
  const at = (iso: string): HumanTurn => ({ ...turn(1), ts: iso });

  it("uses the span the turns cover when it is shorter than the nominal window", () => {
    expect(observedWindowDays([at("2026-10-01T00:00:00Z"), at("2026-10-11T00:00:00Z"), at("2026-10-05T00:00:00Z")], 90)).toBeCloseTo(10, 6);
  });

  it("caps at the nominal window, floors at one day and ignores empty timestamps", () => {
    expect(observedWindowDays([at("2026-10-01T00:00:00Z"), at("2026-10-30T00:00:00Z")], 7)).toBe(7);
    expect(observedWindowDays([at("2026-10-01T00:00:00Z"), at("2026-10-01T00:05:00Z"), at("")], 30)).toBe(1);
    expect(observedWindowDays([at("")], 30)).toBe(30);
  });

  it("feeds the per-30-day estimate: a 10-day corpus under --since 90d is not divided by 90", async () => {
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
    const runner: LabelRunner = async (prompt) => ({ labels: idsIn(prompt).map((id) => ({ id, labels: ["none"] })) });
    const turns = Array.from({ length: 11 }, (_, i) => ({ ...turn(i), ts: `2026-10-${String(1 + i).padStart(2, "0")}T00:00:00Z` }));
    const report = await runLabeling(turns, 11, { n: 11, repeat: 0, model: "sonnet", runner, windowDays: 90 }, out);
    expect(report.wrongApproach.windowDays).toBeCloseTo(10, 6);
  });
});

describe("renderLabeling", () => {
  it("says the patterns are uncalibrated when labeling was off", () => {
    expect(renderLabeling(null).join("\n")).toContain("Patterns are uncalibrated floor counts; run with --label 400 to calibrate.");
  });

  const reportFor = async (labels: string[], repeat: number): Promise<LabelingReport> => {
    const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-"));
    const runner: LabelRunner = async (prompt) => ({ labels: idsIn(prompt).map((id) => ({ id, labels })) });
    return runLabeling(Array.from({ length: 12 }, (_, i) => turn(i)), 12, { n: 12, repeat, model: "sonnet", runner, windowDays: 30 }, out);
  };

  it("renders aggregate numbers, the no-data verdict and n/a agreement without quoting a turn", async () => {
    const failing: LabelRunner = async () => {
      throw new Error("down");
    };
    const empty = await runLabeling([turn(0)], 1, { n: 1, repeat: 0, model: "sonnet", runner: failing, windowDays: 30 }, fs.mkdtempSync(path.join(os.tmpdir(), "label-")));
    const text = renderLabeling(empty).join("\n");
    expect(text).toContain("No labeled turns, so no decision.");
    expect(text).toContain("rigor n/a");
    expect(text).not.toContain("are you sure");
  });

  it("renders the deliverable verdict when design corrections are frequent", async () => {
    const report = await reportFor(["wrong_approach_design"], 12);
    const text = renderLabeling({ ...report, wrongApproach: { ...report.wrongApproach, decision: "deliverable", straddlesThreshold: false } }).join("\n");
    expect(text).toContain("stay a step-3a deliverable");
    expect(text).not.toContain("provisional");
  });

  it("renders the not-a-deliverable verdict with the provisional note", async () => {
    const report = await reportFor(["none"], 0);
    const text = renderLabeling({ ...report, wrongApproach: { ...report.wrongApproach, decision: "not-a-deliverable", straddlesThreshold: true } }).join("\n");
    expect(text).toContain("are not a step-3a deliverable");
    expect(text).toContain("provisional");
  });
});
