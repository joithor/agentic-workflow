import { describe, expect, it } from "vitest";
import { z } from "zod";

import { SindriError } from "../src/errors.js";
import { Budget, childEnv, cliSchema, makeClaudeRunner, meteredRunner, ModelAnswerError, tryRun, type ModelAuditRow, type ModelCall, type ModelRunner, type Spawner } from "../src/scope/model.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { tempDir } from "./helpers.js";

const Answer = z.object({ n: z.number() });
const call = { role: "draft", model: "sonnet", system: "Answer.", input: "count", schema: { type: "object" }, parse: (v: unknown) => Answer.parse(v), timeoutMs: 1000 };
const secret = "AKIA" + "ABCDEFGHIJKLMNOP";

type Seen = { argv: string[]; stdin: string; env: NodeJS.ProcessEnv; cwd: string };
function spawner(answer: { code?: number; stdout?: string; stderr?: string; timedOut?: boolean }): Spawner & { calls: Seen[] } {
  const calls: Seen[] = [];
  const f = (async (argv, o) => {
    calls.push({ argv, stdin: o.stdin, env: o.env, cwd: o.cwd });
    return { code: answer.code ?? 0, stdout: answer.stdout ?? "", stderr: answer.stderr ?? "", timedOut: answer.timedOut ?? false };
  }) as Spawner & { calls: Seen[] };
  f.calls = calls;
  return f;
}

const envelope = (structured: unknown, usage: object = { input_tokens: 100, output_tokens: 20, cache_read_input_tokens: 5, cache_creation_input_tokens: 0 }) =>
  JSON.stringify({ type: "result", structured_output: structured, usage });

function runner(s: Spawner, o: { providers?: string[]; env?: NodeJS.ProcessEnv; allowBaseUrl?: boolean } = {}) {
  const made: string[] = [];
  const removed: string[] = [];
  const r = makeClaudeRunner({
    spawn: s, providers: o.providers ?? ["anthropic"], scrubber: makeScrubber(),
    makeDir: () => { const d = tempDir(); made.push(d); return d; }, removeDir: (d) => { removed.push(d); },
    env: o.env ?? { HOME: "/h" }, effort: "medium", allowBaseUrl: o.allowBaseUrl ?? false,
  });
  return Object.assign(r, { made, removed });
}

describe("Claude model runner", () => {
  it("runs claude -p with no tools, an effort, a schema without $schema, scrubbed stdin, and reports usage", async () => {
    const s = spawner({ stdout: envelope({ n: 3 }) });
    const r = runner(s);
    const out = await r.run({ ...call, input: `count ${secret}`, system: `system ${secret}`, schema: { $schema: "http://json-schema.org/draft-07/schema#", type: "object" } });
    expect(out).toEqual({ value: { n: 3 }, usage: { inputTokens: 105, outputTokens: 20 } });
    const { argv, stdin, env, cwd } = s.calls[0];
    expect(argv.slice(0, 4)).toEqual(["claude", "-p", "--model", "sonnet"]);
    expect(argv[argv.indexOf("--effort") + 1]).toBe("medium");
    expect(argv).toEqual(expect.arrayContaining(["--tools", "", "--strict-mcp-config", "--output-format", "json", "--no-session-persistence"]));
    expect(argv[argv.indexOf("--json-schema") + 1]).toBe('{"type":"object"}');
    expect(argv[argv.indexOf("--system-prompt") + 1]).toBe("system [REDACTED:aws-access-key]");
    expect(stdin).toBe("count [REDACTED:aws-access-key]");
    expect(env).toEqual({ HOME: "/h", AW_JUDGE_CHILD: "1", AW_SINDRI_CHILD: "1" });
    expect(r.made).toEqual([cwd]);
    expect(r.removed).toEqual([cwd]);
  });

  it("isolates the child from user, project and local settings and CLAUDE.md (empty --setting-sources)", async () => {
    const s = spawner({ stdout: envelope({ n: 3 }) });
    await runner(s).run(call);
    const { argv } = s.calls[0];
    const i = argv.indexOf("--setting-sources");
    expect(i).toBeGreaterThan(0);
    expect(argv[i + 1]).toBe("");
    expect(argv).not.toContain("--bare");
  });

  it("falls back to parsing .result, and treats missing usage fields as zero", async () => {
    const withResult = spawner({ stdout: JSON.stringify({ result: '{"n":4}', usage: { input_tokens: 1, output_tokens: 1 } }) });
    expect(await runner(withResult).run(call)).toEqual({ value: { n: 4 }, usage: { inputTokens: 1, outputTokens: 1 } });
    const noUsage = spawner({ stdout: JSON.stringify({ structured_output: { n: 5 } }) });
    expect((await runner(noUsage).run(call)).usage).toEqual({ inputTokens: 0, outputTokens: 0 });
  });

  it("refuses when anthropic isn't an allowed provider (spec §6.1)", () => {
    expect(() => runner(spawner({}), { providers: ["jev"] })).toThrow("providers.allowed doesn't include anthropic");
  });

  it("gives the child an allowlisted environment, and *_BASE_URL only when the profile allows it", async () => {
    const env = { HOME: "/h", PATH: "/bin", LINEAR_TOKEN: "t", ANTHROPIC_BASE_URL: "https://proxy.example", GONE_BASE_URL: undefined };
    expect(childEnv(env, false)).toEqual({ HOME: "/h", PATH: "/bin", AW_JUDGE_CHILD: "1", AW_SINDRI_CHILD: "1" });
    expect(childEnv(env, true)).toEqual({ HOME: "/h", PATH: "/bin", ANTHROPIC_BASE_URL: "https://proxy.example", AW_JUDGE_CHILD: "1", AW_SINDRI_CHILD: "1" });
    const s = spawner({ stdout: envelope({ n: 1 }) });
    await runner(s, { env, allowBaseUrl: true }).run(call);
    expect(s.calls[0].env.ANTHROPIC_BASE_URL).toBe("https://proxy.example");
    expect(s.calls[0].env.LINEAR_TOKEN).toBeUndefined();
    expect(cliSchema({ $schema: "x", type: "object", properties: {} })).toEqual({ type: "object", properties: {} });
  });

  it("maps exits, timeouts, CLI errors, junk and schema mismatches to typed errors (Review Focus 3)", async () => {
    await expect(runner(spawner({ code: 1 })).run(call)).rejects.toThrow("model job failed (claude exited 1)");
    const withStderr = await runner(spawner({ code: 1, stderr: `\nauth failed ${secret}\nmore\n` })).run(call).catch((e: unknown) => e);
    expect((withStderr as SindriError).message).toBe("model job failed (claude exited 1): auth failed [REDACTED:aws-access-key]");
    await expect(runner(spawner({ timedOut: true, code: 137 })).run(call)).rejects.toThrow("model job timed out after 1000 ms");
    for (const stdout of ["not json", "null", "42"]) {
      await expect(runner(spawner({ stdout })).run(call)).rejects.toThrow("model job returned output that isn't JSON");
    }
    const flagged = await runner(spawner({ stdout: JSON.stringify({ is_error: true, result: `Credit balance is too low ${secret}`, usage: {} }) })).run(call).catch((e: unknown) => e);
    expect((flagged as SindriError).message).toBe("model job reported an error: Credit balance is too low [REDACTED:aws-access-key]");
    const words = "lorem ipsum ".repeat(40);
    const long = await runner(spawner({ stdout: JSON.stringify({ is_error: true, result: words }) })).run(call).catch((e: unknown) => e);
    expect((long as SindriError).message).toBe(`model job reported an error: ${words.slice(0, 300)}`);
    const bare = await runner(spawner({ stdout: JSON.stringify({ is_error: true }) })).run(call).catch((e: unknown) => e);
    expect((bare as SindriError).message).toBe("model job reported an error: no message");
    const bad = await runner(spawner({ stdout: envelope({ n: "three" }) })).run(call).catch((e: unknown) => e);
    expect(bad).toBeInstanceOf(ModelAnswerError);
    expect((bad as ModelAnswerError).code).toBe("SND-SCOPE-004");
    expect((bad as ModelAnswerError).usage).toEqual({ inputTokens: 105, outputTokens: 20 });
  });

  it("does not forward secret-looking variables even with base URLs allowed", async () => {
    const key = "ANTHROPIC_" + "API_KEY";
    const env = { HOME: "/h", PATH: "/bin", USER: "u", LANG: "C", TERM: "x", TMPDIR: "/t", [key]: "sk-" + "not-forwarded", GITHUB_TOKEN: "g" };
    const s = spawner({ stdout: envelope({ n: 1 }) });
    await runner(s, { env, allowBaseUrl: true }).run(call);
    expect(s.calls[0].env).toEqual({ HOME: "/h", PATH: "/bin", USER: "u", LANG: "C", TERM: "x", TMPDIR: "/t", AW_JUDGE_CHILD: "1", AW_SINDRI_CHILD: "1" });
    expect(s.calls[0].env[key]).toBeUndefined();
  });

  it("scrubs with the caller's scrubber, so profile extra patterns are redacted before the prompt leaves", async () => {
    const s = spawner({ stdout: envelope({ n: 1 }) });
    const r = makeClaudeRunner({
      spawn: s, providers: ["anthropic"], scrubber: makeScrubber([{ kind: "internal-id", re: /ZQ-\d{6}/ }]),
      makeDir: () => tempDir(), removeDir: () => undefined, env: {}, effort: "low", allowBaseUrl: false,
    });
    await r.run({ ...call, input: "see ZQ-123456 now", system: "sys ZQ-654321" });
    expect(s.calls[0].stdin).toBe("see [REDACTED:internal-id] now");
    expect(s.calls[0].argv[s.calls[0].argv.indexOf("--system-prompt") + 1]).toBe("sys [REDACTED:internal-id]");
  });

  it("charges tokens a reply cost even when its answer is prose, missing, or the CLI flagged an error", async () => {
    const usage = { input_tokens: 7, output_tokens: 3 };
    for (const body of [{ result: "prose", usage }, { usage }]) {
      const e = await runner(spawner({ stdout: JSON.stringify(body) })).run(call).catch((x: unknown) => x);
      expect(e).toBeInstanceOf(ModelAnswerError);
      expect((e as ModelAnswerError).usage).toEqual({ inputTokens: 7, outputTokens: 3 });
    }
    const flagged = await runner(spawner({ stdout: JSON.stringify({ is_error: true, result: "boom", usage }) })).run(call).catch((x: unknown) => x);
    expect((flagged as SindriError).code).toBe("SND-SCOPE-002");
    expect((flagged as SindriError & { usage: unknown }).usage).toEqual({ inputTokens: 7, outputTokens: 3 });
    const budget = new Budget(1000);
    const audit: ModelAuditRow[] = [];
    const metered = meteredRunner(runner(spawner({ stdout: JSON.stringify({ is_error: true, usage }) })), { budget, audit });
    await expect(metered.run(call)).rejects.toThrow("model job reported an error");
    expect([budget.used, audit.length]).toEqual([10, 1]);
  });

  it("clamps junk usage numbers to zero so the budget can't go NaN", async () => {
    const stdout = JSON.stringify({ structured_output: { n: 1 }, usage: { input_tokens: "9", output_tokens: -4, cache_read_input_tokens: 1e999, cache_creation_input_tokens: 2 } });
    expect((await runner(spawner({ stdout })).run(call)).usage).toEqual({ inputTokens: 2, outputTokens: 0 });
  });

  it("removes its scratch directory even when the spawn itself fails", async () => {
    const r = runner((async () => { throw new Error("spawn failed"); }) as Spawner);
    await expect(r.run(call)).rejects.toThrow("spawn failed");
    expect(r.removed).toEqual(r.made);
    expect(r.made).toHaveLength(1);
  });
});

describe("tryRun", () => {
  const usage = { inputTokens: 1, outputTokens: 1 };
  const failing = (e: unknown): ModelRunner => ({ run: async () => { throw e; } });
  const fine: ModelRunner = { async run<T>(c: ModelCall<T>) { return { value: c.parse({ n: 1 }), usage }; } };

  it("returns ok, schema and stop outcomes instead of throwing", async () => {
    expect(await tryRun(fine, call)).toEqual({ kind: "ok", value: { n: 1 } });
    expect(await tryRun(failing(new ModelAnswerError("bad shape", usage)), call)).toEqual({ kind: "schema", reason: "bad shape" });
    expect(await tryRun(failing(new SindriError("SND-SCOPE-005", "token budget exhausted")), call)).toEqual({ kind: "stop", reason: "token budget exhausted", budget: true });
    expect(await tryRun(failing(new SindriError("SND-SCOPE-002", "model job timed out after 1000 ms")), call)).toEqual({ kind: "stop", reason: "model job timed out after 1000 ms", budget: false });
    expect(await tryRun(failing(new Error("boom")), call)).toEqual({ kind: "stop", reason: "boom", budget: false });
    expect(await tryRun(failing(new Error(`spawn ${secret}`)), call)).toEqual({ kind: "stop", reason: "spawn [REDACTED:aws-access-key]", budget: false });
  });
});

describe("Budget and meteredRunner", () => {
  const inner = (u = { inputTokens: 60, outputTokens: 20 }): ModelRunner & { n: number } => {
    const r: ModelRunner & { n: number } = { n: 0, async run<T>(c: ModelCall<T>) { r.n++; return { value: c.parse({ n: 1 }), usage: u }; } };
    return r;
  };

  it("tracks usage and reports exhaustion", () => {
    const b = new Budget(100);
    b.spend({ inputTokens: 60, outputTokens: 20 });
    expect([b.used, b.remaining(), b.exhausted()]).toEqual([80, 20, false]);
    b.spend({ inputTokens: 30, outputTokens: 0 });
    expect([b.remaining(), b.exhausted()]).toEqual([0, true]);
  });

  it("charges the budget and audits every call, tagging the role when asked", async () => {
    const budget = new Budget(100);
    const audit: ModelAuditRow[] = [];
    await meteredRunner(inner(), { budget, audit, tag: "baseline:" }).run(call);
    expect(audit).toEqual([{ role: "baseline:draft", model: "sonnet", inputTokens: 60, outputTokens: 20 }]);
    expect(budget.used).toBe(80);
    const plain: ModelAuditRow[] = [];
    await meteredRunner(inner(), { budget: new Budget(1000), audit: plain }).run(call);
    expect(plain[0].role).toBe("draft");
  });

  it("charges a failed parse (it cost tokens) but not other failures, and refuses when the budget is gone", async () => {
    const budget = new Budget(1000);
    const audit: ModelAuditRow[] = [];
    const parseFail: ModelRunner = { run: async () => { throw new ModelAnswerError("bad", { inputTokens: 30, outputTokens: 0 }); } };
    await expect(meteredRunner(parseFail, { budget, audit }).run(call)).rejects.toBeInstanceOf(ModelAnswerError);
    expect([budget.used, audit.length]).toEqual([30, 1]);
    const timeout: ModelRunner = { run: async () => { throw new SindriError("SND-SCOPE-002", "model job timed out after 1000 ms"); } };
    await expect(meteredRunner(timeout, { budget, audit }).run(call)).rejects.toThrow("timed out");
    expect([budget.used, audit.length]).toEqual([30, 1]);
    const spent = new Budget(10);
    spent.spend({ inputTokens: 10, outputTokens: 0 });
    const never = inner();
    await expect(meteredRunner(never, { budget: spent, audit }).run(call)).rejects.toMatchObject({ code: "SND-SCOPE-005" });
    expect(never.n).toBe(0);
  });
});
