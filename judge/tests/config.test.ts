import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { configPath, DEFAULT_CONFIG, judgeConfigPath, judgeDbPath, judgeStateDir, loadConfig, resolvePromptSort, DEFAULT_PROMPT_SORT } from "../src/config.js";

describe("configPath", () => {
  it("defaults under the given home", () => {
    expect(configPath("/home/user")).toBe("/home/user/.agentic-workflow/judge/config.json");
  });
  it("defaults to os.homedir() when no home is given", () => {
    expect(configPath()).toBe(path.join(os.homedir(), ".agentic-workflow", "judge", "config.json"));
  });
});

describe("judgeStateDir", () => {
  it("defaults to ~/.agentic-workflow when AW_STATE_DIR is unset", () => {
    expect(judgeStateDir({})).toBe(path.join(os.homedir(), ".agentic-workflow"));
  });

  it("uses AW_STATE_DIR when set", () => {
    expect(judgeStateDir({ AW_STATE_DIR: "/tmp/scratch-state" })).toBe("/tmp/scratch-state");
  });

  it("treats an empty-string AW_STATE_DIR as unset", () => {
    expect(judgeStateDir({ AW_STATE_DIR: "" })).toBe(path.join(os.homedir(), ".agentic-workflow"));
  });

  it("defaults to process.env when no env is passed", () => {
    expect(judgeStateDir()).toBe(path.join(os.homedir(), ".agentic-workflow"));
  });
});

describe("judgeConfigPath / judgeDbPath", () => {
  it("join judge/config.json and judge/decisions.sqlite onto the state dir", () => {
    const env = { AW_STATE_DIR: "/tmp/scratch-state" };
    expect(judgeConfigPath(env)).toBe("/tmp/scratch-state/judge/config.json");
    expect(judgeDbPath(env)).toBe("/tmp/scratch-state/judge/decisions.sqlite");
  });

  it("default to the real state dir when AW_STATE_DIR is unset", () => {
    expect(judgeConfigPath({})).toBe(path.join(os.homedir(), ".agentic-workflow", "judge", "config.json"));
    expect(judgeDbPath({})).toBe(path.join(os.homedir(), ".agentic-workflow", "judge", "decisions.sqlite"));
  });
});

describe("loadConfig", () => {
  it("returns defaults when the file is missing", () => {
    expect(loadConfig(path.join(os.tmpdir(), "judge-config-missing-" + Date.now() + ".json"))).toEqual(DEFAULT_CONFIG);
  });

  it("merges a partial file over defaults, question by question", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: { "wake-gate": { enabled: false, threshold: 0.9 } } }));
    expect(loadConfig(file)).toEqual({ questions: { "wake-gate": { enabled: false, threshold: 0.9 } } });
  });

  it("keeps a question's providers list and drops unknown names; leaves non-object entries alone", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: {
      a: { enabled: true, threshold: 0.7, providers: ["jev", "rules"] },
      b: { enabled: true, threshold: 0.7, providers: ["jev", "bogus"] },
      c: null,
    } }));
    const q = loadConfig(file).questions;
    expect(q.a?.providers).toEqual(["jev", "rules"]);
    expect(q.b?.providers).toEqual(["jev"]);
    expect(q.c).toBeNull();
  });

  it("treats a non-array or fully-filtered providers value as unset", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: {
      s: { enabled: true, threshold: 0.7, providers: "jev" },
      o: { enabled: true, threshold: 0.7, providers: {} },
      e: { enabled: true, threshold: 0.7, providers: ["jev-api"] },
    } }));
    const q = loadConfig(file).questions;
    expect(q.s).toEqual({ enabled: true, threshold: 0.7 });
    expect(q.o).toEqual({ enabled: true, threshold: 0.7 });
    expect(q.e).toEqual({ enabled: true, threshold: 0.7 });
  });

  it("merges a file that overrides only one of two default questions, keeping the other default intact", () => {
    const withTwoDefaults = { questions: { "wake-gate": { enabled: true, threshold: 0.7 }, "other-q": { enabled: true, threshold: 0.5 } } };
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: { "wake-gate": { enabled: false, threshold: 0.99 } } }));
    expect(loadConfig(file, withTwoDefaults)).toEqual({
      questions: {
        "wake-gate": { enabled: false, threshold: 0.99 },
        "other-q": { enabled: true, threshold: 0.5 },
      },
    });
  });

  it("falls back to defaults on invalid JSON, without throwing", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, "{not json");
    expect(loadConfig(file)).toEqual(DEFAULT_CONFIG);
  });

  it("falls back to defaults when the top-level JSON value itself isn't an object", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, "5");
    expect(loadConfig(file)).toEqual(DEFAULT_CONFIG);
  });

  it("falls back to defaults when questions is not an object", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify({ questions: "nope" }));
    expect(loadConfig(file)).toEqual(DEFAULT_CONFIG);
  });
});

describe("loadConfig — providers block", () => {
  function write(contents: unknown): string {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "judge-cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify(contents));
    return file;
  }

  it("has no providers key by default (the chain is auto-detected)", () => {
    expect(DEFAULT_CONFIG.providers).toBeUndefined();
  });

  it("loads agentClis order and jev toggle, with questions defaulted when absent", () => {
    expect(loadConfig(write({ providers: { agentClis: ["cursor-cli", "claude-cli"], jev: false } }))).toEqual({
      questions: DEFAULT_CONFIG.questions,
      providers: { agentClis: ["cursor-cli", "claude-cli"], jev: false },
    });
  });

  it("drops unknown provider names and wrong-typed fields instead of failing", () => {
    expect(loadConfig(write({ providers: { agentClis: ["codex-cli", "gemini-cli", 3], jev: "no" } })).providers).toEqual({ agentClis: ["codex-cli"] });
    expect(loadConfig(write({ providers: { agentClis: "codex-cli", jev: true } })).providers).toEqual({ jev: true });
  });

  it("omits providers entirely when the block is empty, invalid, or not an object", () => {
    expect(loadConfig(write({ providers: {} })).providers).toBeUndefined();
    expect(loadConfig(write({ providers: { agentClis: 1 } })).providers).toBeUndefined();
    expect(loadConfig(write({ providers: ["codex-cli"] })).providers).toBeUndefined();
    expect(loadConfig(write({ providers: null })).providers).toBeUndefined();
  });

  it("an empty agentClis list is kept (explicitly no agent CLIs)", () => {
    expect(loadConfig(write({ providers: { agentClis: [] } })).providers).toEqual({ agentClis: [] });
  });
});

describe("promptSort config", () => {
  const writeCfg = (obj: unknown): string => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "cfg-")), "config.json");
    fs.writeFileSync(file, JSON.stringify(obj));
    return file;
  };

  it("defaults to shadow mode: enabled, every scaffold off", () => {
    expect(resolvePromptSort(DEFAULT_CONFIG)).toEqual(DEFAULT_PROMPT_SORT);
    expect(DEFAULT_PROMPT_SORT).toEqual({ enabled: true, budgetMs: 1000, cooldownPrompts: 5, scaffolds: { brief: false, bugfix: false, "ui-evidence": false, "plan-first": false } });
    expect(loadConfig(writeCfg({ questions: {} })).promptSort).toBeUndefined();
  });

  it("merges a partial block over the defaults, drops wrong types and unknown scaffold ids, clamps the budget", () => {
    const cfg = loadConfig(writeCfg({ questions: {}, promptSort: { enabled: false, budgetMs: 5000, cooldownPrompts: "x", scaffolds: { brief: true, bogus: true, bugfix: "yes" } } }));
    expect(resolvePromptSort(cfg)).toEqual({ enabled: false, budgetMs: 1050, cooldownPrompts: 5, scaffolds: { brief: true, bugfix: false, "ui-evidence": false, "plan-first": false } });
    expect(resolvePromptSort(loadConfig(writeCfg({ questions: {}, promptSort: { budgetMs: 10 } }))).budgetMs).toBe(200);
    expect(loadConfig(writeCfg({ questions: {}, promptSort: 7 })).promptSort).toBeUndefined();
  });

  it("keeps a non-negative cooldown (0 disables it) and drops a negative one", () => {
    expect(resolvePromptSort(loadConfig(writeCfg({ questions: {}, promptSort: { cooldownPrompts: 0 } }))).cooldownPrompts).toBe(0);
    expect(resolvePromptSort(loadConfig(writeCfg({ questions: {}, promptSort: { cooldownPrompts: -2 } }))).cooldownPrompts).toBe(5);
    expect(resolvePromptSort(loadConfig(writeCfg({ questions: {}, promptSort: { scaffolds: [] } }))).scaffolds).toEqual(DEFAULT_PROMPT_SORT.scaffolds);
  });
});
