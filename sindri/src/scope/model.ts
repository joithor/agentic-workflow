import { SindriError } from "../errors.js";
import type { Scrubber } from "../scrub/scrub.js";

export type Spawner = (
  argv: string[],
  o: { stdin: string; cwd: string; timeoutMs: number; env: NodeJS.ProcessEnv },
) => Promise<{ code: number; stdout: string; stderr: string; timedOut: boolean }>;

export interface ModelUsage {
  inputTokens: number;
  outputTokens: number;
}

export interface ModelCall<T> {
  role: string;
  model: string;
  system: string;
  input: string;
  schema: Record<string, unknown>;
  parse: (v: unknown) => T;
  timeoutMs: number;
}

export interface ModelRunner {
  run<T>(call: ModelCall<T>): Promise<{ value: T; usage: ModelUsage }>;
}

// A model answer that doesn't match the schema. It still cost tokens.
export class ModelAnswerError extends SindriError {
  constructor(message: string, readonly usage: ModelUsage) {
    super("SND-SCOPE-004", message);
  }
}

export class Budget {
  used = 0;
  constructor(private readonly limit: number) {}
  remaining(): number {
    return Math.max(0, this.limit - this.used);
  }
  spend(u: ModelUsage): void {
    this.used += u.inputTokens + u.outputTokens;
  }
  exhausted(): boolean {
    return this.remaining() === 0;
  }
}

export interface ModelAuditRow {
  role: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
}

// One budget and one audit trail for every model call of a run. The check is
// before the call; the charge is after it, and a parse failure is charged too.
export function meteredRunner(inner: ModelRunner, o: { budget: Budget; audit: ModelAuditRow[]; tag?: string }): ModelRunner {
  return {
    async run<T>(call: ModelCall<T>) {
      if (o.budget.exhausted()) throw new SindriError("SND-SCOPE-005", "token budget exhausted");
      const charge = (u: ModelUsage): void => {
        o.budget.spend(u);
        o.audit.push({ role: `${o.tag ?? ""}${call.role}`, model: call.model, inputTokens: u.inputTokens, outputTokens: u.outputTokens });
      };
      try {
        const r = await inner.run(call);
        charge(r.usage);
        return r;
      } catch (e) {
        if (e instanceof ModelAnswerError) charge(e.usage);
        throw e;
      }
    },
  };
}

export type Outcome<T> =
  | { kind: "ok"; value: T }
  | { kind: "schema"; reason: string }
  | { kind: "stop"; reason: string; budget: boolean };

// The error policy every model loop shares: a bad answer is a round with a
// reason; a refused budget, a timeout or a CLI failure stops the loop.
export async function tryRun<T>(runner: ModelRunner, call: ModelCall<T>): Promise<Outcome<T>> {
  try {
    return { kind: "ok", value: (await runner.run(call)).value };
  } catch (err) {
    if (err instanceof SindriError && err.code === "SND-SCOPE-004") return { kind: "schema", reason: err.message };
    if (err instanceof SindriError && err.code === "SND-SCOPE-005") return { kind: "stop", reason: "token budget exhausted", budget: true };
    return { kind: "stop", reason: (err as Error).message, budget: false };
  }
}

const KEEP_ENV = ["HOME", "PATH", "USER", "LANG", "TERM", "TMPDIR"];

// The child sees only what it needs. ANTHROPIC_BASE_URL and friends can reroute
// model traffic, so they are dropped unless the profile allows them (spec §6.1).
export function childEnv(env: NodeJS.ProcessEnv, allowBaseUrl: boolean): NodeJS.ProcessEnv {
  const out: NodeJS.ProcessEnv = {};
  for (const k of KEEP_ENV) {
    if (env[k] !== undefined) out[k] = env[k];
  }
  if (allowBaseUrl) {
    for (const [k, v] of Object.entries(env)) {
      if (k.endsWith("_BASE_URL") && v !== undefined) out[k] = v;
    }
  }
  return { ...out, AW_JUDGE_CHILD: "1", AW_SINDRI_CHILD: "1" };
}

// zod-to-json-schema adds a $schema key that the CLI's validator doesn't need.
export function cliSchema(schema: Record<string, unknown>): Record<string, unknown> {
  const { $schema: _unused, ...rest } = schema;
  return rest;
}

interface Envelope {
  structured_output?: unknown;
  result?: unknown;
  is_error?: unknown;
  usage?: { input_tokens?: number; output_tokens?: number; cache_read_input_tokens?: number; cache_creation_input_tokens?: number };
}

// The only path from sindri code to a model (spec §6.1 providers, §8.4 egress
// scrub). A bounded job: no tools, no MCP servers, hooks off, fresh empty cwd,
// structured output against a JSON schema. Same invocation judge uses.
export function makeClaudeRunner(o: {
  spawn: Spawner;
  providers: readonly string[];
  scrubber: Scrubber;
  makeDir: () => string;
  removeDir: (dir: string) => void;
  env: NodeJS.ProcessEnv;
  effort: string;
  allowBaseUrl: boolean;
}): ModelRunner {
  if (!o.providers.includes("anthropic")) {
    throw new SindriError("SND-SCOPE-001", "providers.allowed doesn't include anthropic", { fix: "add anthropic to providers.allowed, then sindri profile approve" });
  }
  const scrub = (s: string): string => o.scrubber.scrub(s).text;
  const notJson = (): SindriError => new SindriError("SND-SCOPE-002", "model job returned output that isn't JSON");
  return {
    async run<T>(call: ModelCall<T>) {
      const argv = [
        "claude", "-p", "--model", call.model, "--effort", o.effort, "--no-session-persistence", "--strict-mcp-config", "--mcp-config", '{"mcpServers":{}}',
        "--settings", '{"disableAllHooks":true}', "--disable-slash-commands", "--tools", "",
        "--system-prompt", scrub(call.system), "--json-schema", JSON.stringify(cliSchema(call.schema)), "--output-format", "json",
      ];
      const cwd = o.makeDir();
      let r: Awaited<ReturnType<Spawner>>;
      try {
        r = await o.spawn(argv, { stdin: scrub(call.input), cwd, timeoutMs: call.timeoutMs, env: childEnv(o.env, o.allowBaseUrl) });
      } finally {
        o.removeDir(cwd);
      }
      if (r.timedOut) throw new SindriError("SND-SCOPE-002", `model job timed out after ${call.timeoutMs} ms`);
      if (r.code !== 0) {
        const hint = scrub(r.stderr.split("\n").find((l) => l.trim() !== "") ?? "").slice(0, 200);
        throw new SindriError("SND-SCOPE-002", `model job failed (claude exited ${r.code})${hint === "" ? "" : `: ${hint}`}`);
      }
      let parsed: unknown;
      try {
        parsed = JSON.parse(r.stdout);
      } catch {
        throw notJson();
      }
      if (typeof parsed !== "object" || parsed === null) throw notJson();
      const env = parsed as Envelope;
      if (env.is_error === true) {
        const text = scrub(typeof env.result === "string" ? env.result : "").slice(0, 300);
        throw new SindriError("SND-SCOPE-002", `model job reported an error: ${text === "" ? "no message" : text}`);
      }
      let answer: unknown;
      try {
        answer = env.structured_output ?? JSON.parse(String(env.result));
      } catch {
        throw notJson();
      }
      const u = env.usage ?? {};
      const usage = {
        inputTokens: (u.input_tokens ?? 0) + (u.cache_read_input_tokens ?? 0) + (u.cache_creation_input_tokens ?? 0),
        outputTokens: u.output_tokens ?? 0,
      };
      try {
        return { value: call.parse(answer), usage };
      } catch (e) {
        throw new ModelAnswerError(`the model's answer didn't match the schema: ${scrub((e as Error).message).slice(0, 300)}`, usage);
      }
    },
  };
}
