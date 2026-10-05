// judge/tests/prompt-sort/commands.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { DEFAULT_CONFIG, loadConfig, resolvePromptSort } from "../../src/config.js";
import { openDb } from "../../src/db.js";
import { runPromptSortCommand, type PromptSortCliDeps } from "../../src/prompt-sort/commands.js";
import { deps as jevDeps, jevBody, jevFetch } from "./fixtures.js";

function make(over: Partial<PromptSortCliDeps> = {}): PromptSortCliDeps {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "psc-"));
  return {
    db: openDb(":memory:"), config: DEFAULT_CONFIG, configFile: path.join(dir, "judge", "config.json"), stateDir: dir,
    jev: jevDeps(jevFetch(jevBody())), readStdin: async () => JSON.stringify({ prompt: "Fix the login button crash", sessionId: "s1" }),
    now: () => new Date("2026-10-04T10:00:00.000Z"), randomId: () => "d1", ...over,
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

  it("rejects an unknown subcommand", async () => {
    expect(await runPromptSortCommand(["bogus"], make())).toMatchObject({ exitCode: 1, stderr: "unknown prompt-sort subcommand: bogus" });
  });
});
