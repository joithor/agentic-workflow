import { describe, expect, it } from "vitest";

import { parseArgs } from "../src/args.js";

const NOW = new Date("2026-09-26T12:00:00.000Z");
const HOME = "/home/j";

describe("parseArgs", () => {
  it("parses the audit command with defaults", () => {
    const r = parseArgs(["audit"], NOW, HOME);
    expect(r.ok && r.options.command).toBe("audit");
    expect(r.ok && r.options.auditOut).toBe("/home/j/.agentic-workflow/audit");
    expect(r.ok && r.options.itemPattern).toBe("[A-Z][A-Z0-9]{1,9}-\\d+");
    expect(r.ok && r.options.maxSize).toBe("XS");
  });

  it("parses the audit flags", () => {
    const r = parseArgs(["audit", "--out", "/o", "--items", "/i.json", "--item-pattern", "X-\\d+", "--max-size", "M"], NOW, HOME);
    expect(r.ok && [r.options.auditOut, r.options.itemsFile, r.options.itemPattern, r.options.maxSize]).toEqual(["/o", "/i.json", "X-\\d+", "M"]);
  });

  it("rejects an invalid --max-size and an invalid --item-pattern", () => {
    expect(parseArgs(["audit", "--max-size", "XXL"], NOW, HOME).ok).toBe(false);
    expect(parseArgs(["audit", "--item-pattern", "("], NOW, HOME).ok).toBe(false);
  });
  it("defaults to a one-day report", () => {
    expect(parseArgs([], NOW, HOME)).toEqual({ ok: true, options: {
      command: "report", since: new Date("2026-09-25T12:00:00.000Z"), until: NOW,
      projectsDir: "/home/j/.claude/projects", codexSessionsDir: "/home/j/.codex/sessions", cursorProjectsDir: "/home/j/.cursor/projects",
      providers: null, stateDir: "/home/j/.agentic-workflow", stateDirExplicit: false, prLookup: true,
      contextTokensPath: null, liveSession: null, liveCwd: null, liveWindow: 200_000, json: false,
      auditOut: "/home/j/.agentic-workflow/audit", itemPattern: "[A-Z][A-Z0-9]{1,9}-\\d+", itemsFile: null, maxSize: "XS",
    } });
  });

  it("marks stateDirExplicit true only when --state-dir is actually passed", () => {
    const withoutFlag = parseArgs([], NOW, HOME);
    const withFlag = parseArgs(["--state-dir", "/s"], NOW, HOME);
    expect(withoutFlag.ok && withoutFlag.options.stateDirExplicit).toBe(false);
    expect(withFlag.ok && withFlag.options.stateDirExplicit).toBe(true);
  });

  it("accepts relative and absolute --since", () => {
    const r1 = parseArgs(["--since", "7d"], NOW, HOME);
    const r2 = parseArgs(["--since", "6h"], NOW, HOME);
    const r3 = parseArgs(["--since", "2026-09-01"], NOW, HOME);
    expect(r1.ok && r1.options.since.toISOString()).toBe("2026-09-19T12:00:00.000Z");
    expect(r2.ok && r2.options.since.toISOString()).toBe("2026-09-26T06:00:00.000Z");
    expect(r3.ok && r3.options.since.toISOString()).toBe("2026-09-01T00:00:00.000Z");
  });

  it("accepts the probe command and directory overrides", () => {
    const r = parseArgs(["probe", "--projects-dir", "/p", "--state-dir", "/s", "--no-pr-lookup"], NOW, HOME);
    expect(r).toEqual({ ok: true, options: expect.objectContaining({ command: "probe", projectsDir: "/p", stateDir: "/s", prLookup: false }) });
  });

  it("accepts the context-tokens command with a path", () => {
    const r = parseArgs(["context-tokens", "/tmp/t.jsonl"], NOW, HOME);
    expect(r).toEqual({ ok: true, options: expect.objectContaining({ command: "context-tokens", contextTokensPath: "/tmp/t.jsonl" }) });
  });

  it("accepts the live command with its flags", () => {
    const r = parseArgs(["live", "--session", "abc-123_4.5", "--cwd", "/repo", "--window", "1000000", "--json", "--state-dir", "/s"], NOW, HOME);
    expect(r).toEqual({ ok: true, options: expect.objectContaining({
      command: "live", liveSession: "abc-123_4.5", liveCwd: "/repo", liveWindow: 1_000_000, json: true, stateDir: "/s", stateDirExplicit: true,
    }) });
  });

  it("accepts --provider (single, list, all) and per-provider directories", () => {
    const one = parseArgs(["--provider", "codex", "--codex-dir", "/c", "--cursor-dir", "/k"], NOW, HOME);
    expect(one).toEqual({ ok: true, options: expect.objectContaining({ providers: ["codex"], codexSessionsDir: "/c", cursorProjectsDir: "/k" }) });
    const list = parseArgs(["--provider", "cursor, claude,cursor"], NOW, HOME);
    expect(list.ok && list.options.providers).toEqual(["cursor", "claude"]);
    const all = parseArgs(["--provider", "all"], NOW, HOME);
    expect(all.ok && all.options.providers).toEqual(["claude", "codex", "cursor"]);
  });

  it.each([
    [["--provider", "gemini"], "--provider must be claude|codex|cursor|all (comma-separated ok): gemini"],
    [["--provider"], "--provider needs a value"],
    [["--since"], "--since needs a value"],
    [["--since", "yesterday"], "--since must be like 7d, 12h or an ISO date: yesterday"],
    [["--since", "2027-01-01"], "--since must be in the past: 2027-01-01"],
    [["--bogus"], "unknown argument: --bogus"],
    [["--projects-dir"], "--projects-dir needs a value"],
    [["context-tokens"], "context-tokens needs a path"],
    [["live"], "live needs --session <id>"],
    [["live", "--session"], "--session needs a value"],
    [["live", "--session", "../etc/passwd"], "--session must be letters, digits, '.', '_' or '-': ../etc/passwd"],
    [["live", "--session", "a b"], "--session must be letters, digits, '.', '_' or '-': a b"],
    [["live", "--session", "s1", "--window", "0"], "--window must be a positive integer: 0"],
    [["live", "--session", "s1", "--window", "big"], "--window must be a positive integer: big"],
    [["live", "--session", "s1", "--window", "1.5"], "--window must be a positive integer: 1.5"],
    [["live", "--session", "a/b"], "--session must be letters, digits, '.', '_' or '-': a/b"],
    [["live", "--session", ""], "--session must be letters, digits, '.', '_' or '-': "],
  ])("rejects %j", (argv, error) => {
    expect(parseArgs(argv, NOW, HOME)).toEqual({ ok: false, error });
  });
});
