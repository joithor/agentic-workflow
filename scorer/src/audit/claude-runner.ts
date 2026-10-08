import { execFile } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { LabelerError, type LabelRunner } from "./labels.js";

export interface ExecOptions {
  timeout: number;
  env: NodeJS.ProcessEnv;
  maxBuffer: number;
  cwd: string;
}
export type ExecFn = (file: string, args: string[], opts: ExecOptions, input: string) => Promise<string>;

// The child gets PATH, HOME and USER plus what claude needs to authenticate. Nothing else leaks in.
const ENV_ALLOWLIST = ["PATH", "HOME", "USER", "ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CONFIG_DIR"] as const;

export function childEnv(env: NodeJS.ProcessEnv): NodeJS.ProcessEnv {
  const out: NodeJS.ProcessEnv = {};
  for (const k of ENV_ALLOWLIST) {
    const v = env[k];
    if (v !== undefined) out[k] = v;
  }
  return out;
}

// Print mode, JSON envelope, schema-constrained output, no tools, no MCP servers, and no session file
// (so the labeler's own prompts never land in ~/.claude/projects and get audited as human turns).
// --safe-mode turns off hooks, CLAUDE.md, skills and plugins while auth keeps working. Without it the user's
// UserPromptSubmit hooks (judge sorting, probe logging, memory servers) would receive the turn text and could
// forward it to other providers or write it to logs. --bare is not usable: it never reads the OAuth login.
// All flags were checked against `claude --help` on 2.1.294 (free: no model call). claudeArgs is pinned by a
// test, so change the code and the test together if a flag differs on the installed version.
export function claudeArgs(model: string, schema: object): string[] {
  return ["-p", "--output-format", "json", "--json-schema", JSON.stringify(schema), "--model", model, "--tools", "", "--strict-mcp-config", "--no-session-persistence", "--safe-mode"];
}

// `--output-format json` wraps the answer in an envelope. With --json-schema the parsed object is in
// `structured_output`; otherwise the model's text is in `result`.
export function extractStructured(stdout: string): unknown {
  let envelope: { is_error?: unknown; result?: unknown; structured_output?: unknown };
  try {
    envelope = JSON.parse(stdout) as typeof envelope;
  } catch (e) {
    throw new LabelerError("claude output was not valid JSON", e);
  }
  if (envelope.is_error === true) throw new LabelerError("claude reported an error");
  if (typeof envelope.structured_output === "object" && envelope.structured_output !== null) return envelope.structured_output;
  if (typeof envelope.result === "string") {
    try {
      return JSON.parse(envelope.result);
    } catch (e) {
      throw new LabelerError("claude result was not valid JSON", e);
    }
  }
  throw new LabelerError("claude output has no structured_output or result");
}

// A failed spawn becomes a fixed reason: the raw error carries the command line and stderr.
function execFailure(e: unknown): LabelerError {
  const err = e as { code?: unknown; killed?: unknown };
  if (err.code === "ENOENT") return new LabelerError("claude CLI not found", e);
  if (err.killed === true) return new LabelerError("claude timed out", e);
  if (typeof err.code === "number") return new LabelerError(`claude exited with code ${err.code}`, e);
  return new LabelerError("claude could not be run", e);
}

// execFile, never a shell. The prompt goes on stdin so its size never hits the argv limit.
export const execClaude: ExecFn = (file, args, opts, input) =>
  new Promise<string>((resolve, reject) => {
    const child = execFile(file, args, { timeout: opts.timeout, env: opts.env, maxBuffer: opts.maxBuffer, cwd: opts.cwd }, (err, stdout) => {
      if (err) reject(err);
      else resolve(stdout);
    });
    child.stdin?.on("error", () => undefined); // EPIPE when the child exits before reading
    child.stdin?.end(input);
  });

export function makeClaudeRunner(opts: { model: string; timeoutMs?: number; env?: NodeJS.ProcessEnv; exec?: ExecFn }): LabelRunner {
  const exec = opts.exec ?? execClaude;
  const env = childEnv(opts.env ?? process.env);
  // A fresh private directory, never the shared temp root: no other user's .claude/ or CLAUDE.md can be picked up.
  const cwd = fs.mkdtempSync(path.join(os.tmpdir(), "audit-label-"));
  return async (prompt, schema) => {
    let stdout: string;
    try {
      stdout = await exec("claude", claudeArgs(opts.model, schema), { timeout: opts.timeoutMs ?? 180_000, env, maxBuffer: 20_000_000, cwd }, prompt);
    } catch (e) {
      throw execFailure(e);
    }
    return extractStructured(stdout);
  };
}
