// judge/tests/prompt-sort/commands.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { DEFAULT_CONFIG, loadConfig, resolvePromptSort } from "../../src/config.js";
import { labeledItems, openDb, recordLabel, upsertEvalItem } from "../../src/db.js";
import { fakeProvider } from "../helpers.js";
import { runPromptSortCommand, type PromptSortCliDeps } from "../../src/prompt-sort/commands.js";
import { renderSortEval } from "../../src/prompt-sort/eval-run.js";
import { deps as jevDeps, jevBody, jevFetch } from "./fixtures.js";

function make(over: Partial<PromptSortCliDeps> = {}): PromptSortCliDeps {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "psc-"));
  return {
    db: openDb(":memory:"), config: DEFAULT_CONFIG, configFile: path.join(dir, "judge", "config.json"), stateDir: dir,
    jev: jevDeps(jevFetch(jevBody())), readStdin: async () => JSON.stringify({ prompt: "Fix the login button crash", sessionId: "s1" }),
    now: () => new Date("2026-10-04T10:00:00.000Z"), randomId: () => "d1", projectsDir: dir, evalsDir: fs.mkdtempSync(path.join(os.tmpdir(), "evals-")), adjudicator: null, ...over,
  };
}

describe("runPromptSortCommand", () => {
  it("with no subcommand reads stdin JSON and sorts", async () => {
    const r = await runPromptSortCommand([], make());
    expect(r.exitCode).toBe(0);
    expect(JSON.parse(r.stdout)).toMatchObject({ id: "d1", fired: [] });
  });

  it("rejects stdin that is not JSON with exit 1", async () => {
    expect(await runPromptSortCommand([], make({ readStdin: async () => "nope" }))).toMatchObject({ exitCode: 1, stderr: "input is not valid JSON" });
  });

  it("scaffold <id> on|off writes the switch to config.json and preserves other config", async () => {
    const d = make();
    fs.mkdirSync(path.dirname(d.configFile), { recursive: true });
    fs.writeFileSync(d.configFile, JSON.stringify({ questions: { "wake-gate": { enabled: true, threshold: 0.7 } }, providers: { jev: false } }));
    const on = await runPromptSortCommand(["scaffold", "ui-evidence", "on"], d);
    expect(on.exitCode).toBe(0);
    const cfg = loadConfig(d.configFile);
    expect(resolvePromptSort(cfg).scaffolds["ui-evidence"]).toBe(true);
    expect(cfg.providers).toEqual({ jev: false });
    expect(cfg.questions["wake-gate"]).toEqual({ enabled: true, threshold: 0.7 });
    await runPromptSortCommand(["scaffold", "ui-evidence", "off"], d);
    expect(resolvePromptSort(loadConfig(d.configFile)).scaffolds["ui-evidence"]).toBe(false);
  });

  it("scaffold on creates a missing config file", async () => {
    const d = make();
    expect((await runPromptSortCommand(["scaffold", "brief", "on"], d)).exitCode).toBe(0);
    expect(resolvePromptSort(loadConfig(d.configFile)).scaffolds.brief).toBe(true);
  });

  it("rejects an unknown scaffold or switch value", async () => {
    expect(await runPromptSortCommand(["scaffold", "nope", "on"], make())).toMatchObject({ exitCode: 1, stderr: "usage: judge prompt-sort scaffold <brief|bugfix|ui-evidence|plan-first> <on|off>" });
    expect(await runPromptSortCommand(["scaffold", "brief", "maybe"], make())).toMatchObject({ exitCode: 1 });
    expect(await runPromptSortCommand(["scaffold"], make())).toMatchObject({ exitCode: 1 });
  });

  it("why <id> prints the decision, its axes and its run", async () => {
    const d = make();
    await runPromptSortCommand([], d);
    const r = await runPromptSortCommand(["why", "d1"], d);
    const body = JSON.parse(r.stdout) as { decision: { question: string }; axes: unknown[]; run: { mode: string } };
    expect(body.decision.question).toBe("prompt-sort");
    expect(body.axes).toHaveLength(11);
    expect(body.run.mode).toBe("blend");
    expect(await runPromptSortCommand(["why", "nope"], d)).toMatchObject({ exitCode: 1, stderr: "unknown prompt-sort decision: nope" });
    expect(await runPromptSortCommand(["why"], d)).toMatchObject({ exitCode: 1 });
  });

  it("why refuses a decision of another question", async () => {
    const d = make();
    d.db.prepare("INSERT INTO decisions (id, ts, question, content_class, provider, decision, confidence, reason_code, latency_ms, input_digest) VALUES ('w','t','wake-gate','x','jev','send',1,'r',1,'d')").run();
    expect(await runPromptSortCommand(["why", "w"], d)).toMatchObject({ exitCode: 1 });
  });

  it("import --since 14d turns a recorded decision into 11 eval items; --since defaults to 14d", async () => {
    const d = make();
    await runPromptSortCommand([], d);
    expect(JSON.parse((await runPromptSortCommand(["import", "--since", "14d"], d)).stdout)).toEqual({ imported: 11, contaminated: 0, existing: 0 });
    expect(JSON.parse((await runPromptSortCommand(["import"], d)).stdout)).toEqual({ imported: 0, contaminated: 0, existing: 11 });
    const noClock = make({ now: undefined });
    expect(JSON.parse((await runPromptSortCommand(["import"], noClock)).stdout)).toEqual({ imported: 0, contaminated: 0, existing: 0 });
  });

  it("outcomes exits 1 without a transcripts dir, otherwise labels", async () => {
    const missing = make({ projectsDir: "/nonexistent/projects" });
    expect(await runPromptSortCommand(["outcomes"], missing)).toMatchObject({ exitCode: 1, stderr: "no transcripts at /nonexistent/projects" });
    const d = make();
    expect(JSON.parse((await runPromptSortCommand(["outcomes"], d)).stdout)).toEqual({ labeled: 0, noSignal: 0, pending: 0 });
    // With a real transcript the default clock is used and the positive label lands.
    fs.mkdirSync(path.join(d.projectsDir, "-repo"));
    fs.writeFileSync(path.join(d.projectsDir, "-repo", "s1.jsonl"), '{"type":"assistant","timestamp":"2026-10-04T10:01:00.000Z","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"/r/a.ts"}}]}}');
    await runPromptSortCommand([], d);
    await runPromptSortCommand(["import"], d);
    expect(JSON.parse((await runPromptSortCommand(["outcomes"], { ...d, now: undefined })).stdout)).toMatchObject({ labeled: 3 });
  });

  it("adjudicate exits 1 without the claude CLI, otherwise labels up to --limit (default 80)", async () => {
    expect(await runPromptSortCommand(["adjudicate"], make())).toMatchObject({ exitCode: 1, stderr: "claude CLI not found on PATH (the adjudicator needs it)" });
    const reply = { status: "decided" as const, decision: "ok", confidence: 1, reason_code: "claude-cli", extra: { is_task: "yes", complexity: "small" } };
    const d = make({ adjudicator: () => fakeProvider("claude-cli", ["brief"], reply as never) });
    await runPromptSortCommand([], d);
    await runPromptSortCommand(["import"], d);
    expect(JSON.parse((await runPromptSortCommand(["adjudicate", "--limit", "1"], d)).stdout)).toMatchObject({ prompts: 1, labeled: 2 });
    expect(labeledItems(d.db, "prompt-sort:is_task", "adjudicator").map((r) => r.label)).toEqual(["yes"]);
    expect(JSON.parse((await runPromptSortCommand(["adjudicate"], { ...d, now: undefined, adjudicator: () => fakeProvider("claude-cli", ["brief"], { ...reply, extra: { wants_loop: "no" } } as never) })).stdout)).toMatchObject({ prompts: 1, labeled: 1, split: 8 });
  });

  it("rejects an unknown subcommand", async () => {
    expect(await runPromptSortCommand(["bogus"], make())).toMatchObject({ exitCode: 1, stderr: "unknown prompt-sort subcommand: bogus" });
  });

  it("eval writes the json and markdown files into evalsDir and prints the json", async () => {
    const d = make();
    upsertEvalItem(d.db, { id: "i1", question: "prompt-sort:touches_ui", input_json: JSON.stringify({ prompt: "Update the shift export script" }), source: "decision:p1:touches_ui", model_decision: null, created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(d.db, "i1", "no", "2026-10-01T00:00:00.000Z", "adjudicator");
    const r = await runPromptSortCommand(["eval"], d);
    expect(r.exitCode).toBe(0);
    const printed = JSON.parse(r.stdout);
    expect(printed.ranAt).toBe("2026-10-04T10:00:00.000Z");
    const jsonFile = path.join(d.evalsDir, "prompt-sort-2026-10-04T10-00-00-000Z.json");
    expect(JSON.parse(fs.readFileSync(jsonFile, "utf8"))).toEqual(printed);
    expect(fs.readFileSync(jsonFile.replace(/\.json$/, ".md"), "utf8")).toContain("| Axis | Source |");
  });

  it("eval defaults the clock to now", async () => {
    const r = await runPromptSortCommand(["eval"], make({ now: undefined }));
    expect(JSON.parse(r.stdout).summaries).toEqual([]);
  });

  it("promote with no eval file exits 1, whether the dir is missing or empty", async () => {
    const msg = "no prompt-sort eval found (run: judge prompt-sort eval)";
    const d = make();
    expect(await runPromptSortCommand(["promote"], { ...d, evalsDir: path.join(d.evalsDir, "missing") })).toMatchObject({ exitCode: 1, stderr: msg });
    expect(await runPromptSortCommand(["promote"], d)).toMatchObject({ exitCode: 1, stderr: msg });
  });

  it("promote reads the newest eval file; --apply flips only the go scaffolds", async () => {
    const d = make();
    const sum = (source: "adjudicator" | "outcome", n: number) => ({ axis: "touches_ui", source, n, heuristic: { accuracy: 0.5, ppv: 0.5 }, blend: { accuracy: 0.9, ppv: 0.9 } });
    const passing = { ranAt: "2026-10-04T00:00:00.000Z", summaries: [sum("adjudicator", 80), sum("outcome", 50)], agreement: [{ axis: "touches_ui", shared: 30, agreed: 28 }] };
    fs.writeFileSync(path.join(d.evalsDir, "prompt-sort-2026-10-03T00-00-00-000Z.json"), JSON.stringify({ ranAt: "old", summaries: [], agreement: [] }));
    fs.writeFileSync(path.join(d.evalsDir, "prompt-sort-2026-10-04T00-00-00-000Z.json"), JSON.stringify(passing));
    fs.writeFileSync(path.join(d.evalsDir, "prompt-sort-2026-10-04T00-00-00-000Z.md"), "ignored");

    const dry = JSON.parse((await runPromptSortCommand(["promote"], d)).stdout);
    expect(dry.evalFile).toBe("prompt-sort-2026-10-04T00-00-00-000Z.json");
    expect(dry.turnedOn).toEqual([]);
    expect(dry.verdicts.find((v: { scaffold: string }) => v.scaffold === "ui-evidence").go).toBe(true);
    expect(fs.existsSync(d.configFile)).toBe(false);

    const applied = JSON.parse((await runPromptSortCommand(["promote", "--apply"], d)).stdout);
    expect(applied.turnedOn).toEqual(["ui-evidence"]);
    expect(resolvePromptSort(loadConfig(d.configFile)).scaffolds).toEqual({ brief: false, bugfix: false, "ui-evidence": true, "plan-first": false });
  });
});
