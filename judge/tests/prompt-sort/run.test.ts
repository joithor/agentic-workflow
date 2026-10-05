// judge/tests/prompt-sort/run.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";

import { DEFAULT_CONFIG, jevTimeoutMs, type JudgeConfig } from "../../src/config.js";
import { getDecision, openDb } from "../../src/db.js";
import { runPromptSort, type PromptSortDeps } from "../../src/prompt-sort/run.js";
import { readSortState, sortStatePath, writeSortState } from "../../src/prompt-sort/session-state.js";
import { promptSortAxesFor, promptSortRunFor } from "../../src/prompt-sort/store.js";
import { deps as jevDeps, jevBody, jevFetch, noul } from "./fixtures.js";

const PROMPT = "Fix the login button crash on the settings page";
const ALL_ON: JudgeConfig = { ...DEFAULT_CONFIG, promptSort: { scaffolds: { brief: true, bugfix: true, "ui-evidence": true, "plan-first": true } } };
const make = (over: Partial<PromptSortDeps> = {}): PromptSortDeps => ({
  db: openDb(":memory:"), config: DEFAULT_CONFIG, jev: jevDeps(jevFetch(jevBody())), stateDir: fs.mkdtempSync(path.join(os.tmpdir(), "ps-")),
  randomId: () => "d1", now: () => new Date("2026-10-04T10:00:00.000Z"), ...over,
});
const out = (r: { stdout: string }) => JSON.parse(r.stdout) as Record<string, unknown>;
const failureRows = (d: PromptSortDeps) => d.db.prepare("SELECT question, provider, reason_code FROM failures").all();

describe("runPromptSort fail-open and budget", () => {
  it("returns exit 0 and a skip envelope when a dependency throws unexpectedly", async () => {
    const d = make({ randomId: () => { throw new Error("boom"); } });
    const r = await runPromptSort({ prompt: PROMPT }, d);
    expect(r.exitCode).toBe(0);
    expect(out(r)).toEqual({ skipped: "error" });
  });

  it("caps the Jev timeout so it fits inside the hook kill budget", () => {
    expect(jevTimeoutMs(1000)).toBe(1000);
    expect(jevTimeoutMs(1050)).toBe(1050);
    expect(jevTimeoutMs(5000)).toBe(1050);
    expect(jevTimeoutMs(1000, 1200)).toBe(750);
    expect(jevTimeoutMs(1000, 300)).toBe(50);
  });

  it("passes the capped timeout to the Jev call", async () => {
    const timeouts: number[] = [];
    const real = globalThis.setTimeout;
    const spy = vi.spyOn(globalThis, "setTimeout").mockImplementation(((fn: () => void, ms?: number) => { timeouts.push(ms ?? 0); return real(fn, 0); }) as never);
    try {
      await runPromptSort({ prompt: PROMPT }, make({ hookBudgetMs: 1200 }));
    } finally {
      spy.mockRestore();
    }
    expect(timeouts).toContain(750);
  });
});

describe("runPromptSort", () => {
  it("shadow mode (default config): records the decision, reports would-fire, injects nothing", async () => {
    const d = make();
    const r = await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, d);
    expect(r.exitCode).toBe(0);
    expect(out(r)).toMatchObject({ id: "d1", mode: "blend", fired: [], wouldFire: ["brief", "bugfix", "ui-evidence"], context: "" });
    expect(getDecision(d.db, "d1")).toMatchObject({ question: "prompt-sort", provider: "jev", decision: "small" });
    expect(promptSortAxesFor(d.db, "d1").every((a) => a.status === "agreed")).toBe(true);
    expect(promptSortRunFor(d.db, "d1")?.wouldFire).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect(readSortState(sortStatePath(d.stateDir, "s1") as string)).toMatchObject({ prompts: 1, requirements: {} });
    expect(failureRows(d)).toEqual([]);
  });

  it("with switches on: fires the scaffolds, sets the UI requirement, then cools down but keeps the requirement", async () => {
    const d = make({ config: ALL_ON });
    const first = out(await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, d));
    expect(first.fired).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect(first.context).toContain("Goal");
    expect(first.context).toContain("/bugFixOrchestrator");
    expect(first.context).toContain("UI evidence");
    const file = sortStatePath(d.stateDir, "s1") as string;
    expect(readSortState(file)).toMatchObject({ prompts: 1, lastFired: { brief: 1, bugfix: 1, "ui-evidence": 1 }, requirements: { uiEvidence: true } });

    const second = out(await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, { ...d, randomId: () => "d2" }));
    expect(second).toMatchObject({ fired: [], context: "" });
    expect(readSortState(file)).toMatchObject({ prompts: 2, requirements: { uiEvidence: true } });
  });

  it("clears the UI requirement when a later task prompt is judged not to touch the UI", async () => {
    const d = make({ jev: jevDeps(jevFetch(jevBody({ touches_ui: noul(0.05) }))) });
    const file = sortStatePath(d.stateDir, "s1") as string;
    writeSortState(file, { prompts: 3, lastFired: {}, requirements: { uiEvidence: true } });
    await runPromptSort({ prompt: "Update the shift export script", sessionId: "s1" }, d);
    expect(readSortState(file).requirements).toEqual({});
  });

  it("keeps the UI requirement on a heuristic-only non-UI run (Jev outage), RF-4", async () => {
    const d = make({ jev: jevDeps(vi.fn().mockRejectedValue(new DOMException("aborted", "AbortError"))) });
    const file = sortStatePath(d.stateDir, "s1") as string;
    writeSortState(file, { prompts: 3, lastFired: {}, requirements: { uiEvidence: true } });
    const r = out(await runPromptSort({ prompt: "Update the shift export script", sessionId: "s1" }, d));
    expect(r).toMatchObject({ mode: "heuristic-only" });
    expect(readSortState(file).requirements).toEqual({ uiEvidence: true });
  });

  it("keeps the UI requirement when a judge-seen non-UI prompt names a workflow, RF-4", async () => {
    const d = make({ jev: jevDeps(jevFetch(jevBody({ touches_ui: noul(0.05) }))) });
    const file = sortStatePath(d.stateDir, "s1") as string;
    writeSortState(file, { prompts: 3, lastFired: {}, requirements: { uiEvidence: true } });
    await runPromptSort({ prompt: "Use /rootCause on the shift export script", sessionId: "s1" }, d);
    expect(readSortState(file).requirements).toEqual({ uiEvidence: true });
  });

  it("keeps the UI requirement across a prompt that is not a task", async () => {
    const d = make({ jev: jevDeps(jevFetch(jevBody({ is_task: noul(0.05), touches_ui: noul(0.05) }))) });
    const file = sortStatePath(d.stateDir, "s1") as string;
    writeSortState(file, { prompts: 3, lastFired: {}, requirements: { uiEvidence: true } });
    await runPromptSort({ prompt: "What does the export script do?", sessionId: "s1" }, d);
    expect(readSortState(file).requirements).toEqual({ uiEvidence: true });
  });

  it("is pure heuristics when Jev times out, never fires a scaffold, and records a failures row (RF-2, RF-3, C14)", async () => {
    const d = make({ config: ALL_ON, jev: jevDeps(vi.fn().mockRejectedValue(new DOMException("aborted", "AbortError"))) });
    const r = out(await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, d));
    expect(r).toMatchObject({ mode: "heuristic-only", reason: "timeout", fired: [], context: "" });
    expect(getDecision(d.db, "d1")).toMatchObject({ provider: "rules", reason_code: "heuristic-only:timeout", outcome: "decided" });
    expect(failureRows(d)).toEqual([{ question: "prompt-sort", provider: "jev", reason_code: "timeout" }]);
  });

  it("records a failures row for a Jev HTTP error too, with the decision unchanged (C14)", async () => {
    const d = make({ jev: jevDeps(jevFetch({}, 503)) });
    const r = out(await runPromptSort({ prompt: PROMPT }, d));
    expect(r).toMatchObject({ mode: "heuristic-only", fired: [] });
    expect(getDecision(d.db, "d1")).toMatchObject({ provider: "rules", outcome: "decided" });
    expect(failureRows(d)).toHaveLength(1);
    expect(failureRows(d)[0]).toMatchObject({ question: "prompt-sort", provider: "jev" });
  });

  it("does not record a failure when no judge is configured (unavailable is not an outage)", async () => {
    const d = make({ jev: null });
    expect(out(await runPromptSort({ prompt: PROMPT }, d))).toMatchObject({ mode: "heuristic-only", reason: "no-jev" });
    expect(failureRows(d)).toEqual([]);
  });

  it("still answers when the failures write itself fails (fails open)", async () => {
    const d = make({ jev: jevDeps(jevFetch({}, 503)) });
    d.db.exec("DROP TABLE failures");
    expect(out(await runPromptSort({ prompt: PROMPT }, d))).toMatchObject({ mode: "heuristic-only" });
  });

  it.each([["yes go ahead", "confirmation"], ["/bugFixOrchestrator FRN-1", "slash-command"], ["<system-reminder>x</system-reminder>", "machine"]])(
    "skips %j with no decision row (RF-3)", async (prompt, reason) => {
      const d = make();
      expect(out(await runPromptSort({ prompt }, d))).toEqual({ skipped: reason });
      expect((d.db.prepare("SELECT COUNT(*) n FROM decisions").get() as { n: number }).n).toBe(0);
    },
  );

  it("honours both kill switches", async () => {
    const off = make({ config: { ...DEFAULT_CONFIG, promptSort: { enabled: false } } });
    expect(out(await runPromptSort({ prompt: PROMPT }, off))).toEqual({ skipped: "disabled" });
    const q = make({ config: { questions: { "prompt-sort": { enabled: false, threshold: 0.7 } } } });
    expect(out(await runPromptSort({ prompt: PROMPT }, q))).toEqual({ skipped: "disabled" });
  });

  it("never writes a state file for a hostile, empty or missing session id (RF-4)", async () => {
    const d = make();
    await runPromptSort({ prompt: PROMPT, sessionId: "../../evil" }, d);
    await runPromptSort({ prompt: PROMPT, sessionId: "" }, { ...d, randomId: () => "d3" });
    await runPromptSort({ prompt: PROMPT }, { ...d, randomId: () => "d2" });
    expect(fs.existsSync(path.join(d.stateDir, "judge"))).toBe(false);
    expect(fs.existsSync(path.join(d.stateDir, "..", "evil.sort.json"))).toBe(false);
  });

  it("does not scaffold a prompt that already names a workflow", async () => {
    const d = make({ config: ALL_ON });
    const r = out(await runPromptSort({ prompt: "Fix the login button crash with bugFixOrchestrator", sessionId: "s1" }, d));
    // C8: a prompt naming a workflow suppresses every scaffold, not just bugfix.
    expect(r).toMatchObject({ fired: [], context: "" });
    expect(r.suppressed).toEqual(expect.arrayContaining([{ id: "bugfix", reason: "already-named" }]));
    expect(readSortState(sortStatePath(d.stateDir, "s1") as string).requirements).toEqual({});
  });

  it("still answers when the storage write fails (fails open)", async () => {
    const d = make({ config: ALL_ON });
    d.db.exec("DROP TABLE prompt_sort_axes");
    const r = out(await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, d));
    expect(r.fired).toEqual(["brief", "bugfix", "ui-evidence"]);
    expect((d.db.prepare("SELECT COUNT(*) n FROM decisions").get() as { n: number }).n).toBe(0);
  });

  it("still answers when the session state cannot be written (fails open)", async () => {
    const d = make({ config: ALL_ON });
    fs.writeFileSync(path.join(d.stateDir, "judge"), "a file where the sessions dir should be");
    expect(out(await runPromptSort({ prompt: PROMPT, sessionId: "s1" }, d)).fired).toEqual(["brief", "bugfix", "ui-evidence"]);
  });

  it("generates an id and timestamp when none are injected", async () => {
    const d = make({ randomId: undefined, now: undefined });
    const r = out(await runPromptSort({ prompt: PROMPT }, d));
    expect(r.id).toMatch(/^[0-9a-f-]{36}$/);
    expect(getDecision(d.db, r.id as string)?.ts).toMatch(/^\d{4}-\d\d-\d\dT/);
  });

  it("rejects a payload without a prompt with exit 1", async () => {
    expect(await runPromptSort({ nope: 1 }, make())).toMatchObject({ exitCode: 1, stderr: "invalid prompt-sort input" });
  });
});
