import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import type { ExecFn } from "../src/audit/claude-runner.js";
import { childEnv, claudeArgs, execClaude, extractStructured, makeClaudeRunner } from "../src/audit/claude-runner.js";

const BASE = { timeout: 10_000, env: { PATH: process.env.PATH ?? "" }, maxBuffer: 1_000_000, cwd: os.tmpdir() };

describe("claudeArgs", () => {
  it("runs print mode with JSON output, the schema and model, no tools, no MCP servers, no session file and no hooks or CLAUDE.md (safe mode)", () => {
    expect(claudeArgs("sonnet", { type: "object" })).toEqual([
      "-p", "--output-format", "json", "--json-schema", '{"type":"object"}', "--model", "sonnet", "--tools", "", "--strict-mcp-config", "--no-session-persistence", "--safe-mode",
    ]);
  });
});

describe("childEnv", () => {
  it("passes only PATH, HOME, USER and the auth variables claude needs", () => {
    const env = childEnv({ PATH: "/bin", HOME: "/h", USER: "u", ANTHROPIC_API_KEY: "k", CLAUDE_CODE_OAUTH_TOKEN: "o", CLAUDE_CONFIG_DIR: "/c", AWS_SECRET_ACCESS_KEY: "s", GITHUB_TOKEN: "g" });
    expect(env).toEqual({ PATH: "/bin", HOME: "/h", USER: "u", ANTHROPIC_API_KEY: "k", CLAUDE_CODE_OAUTH_TOKEN: "o", CLAUDE_CONFIG_DIR: "/c" });
  });
});

describe("extractStructured", () => {
  it("prefers structured_output", () => {
    expect(extractStructured(JSON.stringify({ result: "", structured_output: { labels: [] } }))).toEqual({ labels: [] });
  });

  it("falls back to JSON in result", () => {
    expect(extractStructured(JSON.stringify({ result: '{"labels":[]}' }))).toEqual({ labels: [] });
  });

  it("throws on non-JSON, error envelopes and unparseable results", () => {
    expect(() => extractStructured("not json")).toThrow();
    expect(() => extractStructured(JSON.stringify({ is_error: true, result: "boom" }))).toThrow(/boom/);
    expect(() => extractStructured(JSON.stringify({ result: "plain prose" }))).toThrow();
    expect(() => extractStructured(JSON.stringify({ is_error: true }))).toThrow(/unknown/);
    expect(() => extractStructured(JSON.stringify({}))).toThrow(/no structured_output or result/);
  });
});

describe("execClaude", () => {
  it("pipes input to stdin and returns stdout, with no shell expansion", async () => {
    const out = await execClaude(process.execPath, ["-e", "process.stdin.pipe(process.stdout)"], BASE, "hello $(echo injected)");
    expect(out).toBe("hello $(echo injected)");
  });

  it("rejects when the child exceeds the timeout", async () => {
    await expect(execClaude(process.execPath, ["-e", "setTimeout(() => {}, 5000)"], { ...BASE, timeout: 100 }, "")).rejects.toThrow();
  });
});

describe("makeClaudeRunner", () => {
  it("spawns claude without a shell, with the allowlisted env, a timeout, the prompt on stdin", async () => {
    const seen: { file: string; args: string[]; env: NodeJS.ProcessEnv; timeout: number; input: string }[] = [];
    const exec: ExecFn = async (file, args, opts, input) => {
      seen.push({ file, args, env: opts.env, timeout: opts.timeout, input });
      return JSON.stringify({ structured_output: { labels: [] } });
    };
    const run = makeClaudeRunner({ model: "sonnet", env: { PATH: "/bin", HOME: "/h", USER: "u", GITHUB_TOKEN: "g" }, exec });
    expect(await run("the prompt", { type: "object" })).toEqual({ labels: [] });
    expect(seen).toHaveLength(1);
    expect(seen[0].file).toBe("claude");
    expect(seen[0].args).toEqual(claudeArgs("sonnet", { type: "object" }));
    expect(seen[0].env).toEqual({ PATH: "/bin", HOME: "/h", USER: "u" });
    expect(seen[0].timeout).toBe(180_000);
    expect(seen[0].input).toBe("the prompt");
  });

  it("honours timeoutMs and falls back to process.env when no env is given", async () => {
    let timeout = 0;
    let env: NodeJS.ProcessEnv = {};
    const exec: ExecFn = async (_file, _args, opts) => {
      timeout = opts.timeout;
      env = opts.env;
      return JSON.stringify({ structured_output: {} });
    };
    await makeClaudeRunner({ model: "sonnet", timeoutMs: 5, exec })("p", {});
    expect(timeout).toBe(5);
    expect(env.PATH).toBe(process.env.PATH);
    expect(Object.keys(env).every((k) => ["PATH", "HOME", "USER", "ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CONFIG_DIR"].includes(k))).toBe(true);
  });

  it("runs the real spawn path against a stub claude on PATH (no model call), from a private cwd", async () => {
    const bin = fs.mkdtempSync(path.join(os.tmpdir(), "stub-claude-"));
    const stub = path.join(bin, "claude");
    fs.writeFileSync(stub, `#!/bin/sh\ncat >/dev/null\nprintf '{"structured_output":{"cwd":"%s"}}' "$(pwd -P)"\n`, { mode: 0o755 });
    const out = (await makeClaudeRunner({ model: "sonnet", env: { PATH: bin } })("p", {})) as { cwd: string };
    expect(out.cwd).not.toBe(fs.realpathSync(process.cwd()));
    expect(path.basename(out.cwd)).toMatch(/^audit-label-/);
  });
});
