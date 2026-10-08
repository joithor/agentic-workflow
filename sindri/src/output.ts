import { ERRORS, SindriError, type ErrorCode } from "./errors.js";

// Spec §10.3: 0 ok, 1 attention needed, 2 error.
export type ExitCode = 0 | 1 | 2;

export interface CommandResult {
  exitCode: ExitCode;
  stdout: string;
  stderr: string;
}

function line(text: string): string {
  return text.endsWith("\n") ? text : `${text}\n`;
}

export function success(text: string, data: unknown, json: boolean, exitCode: ExitCode = 0): CommandResult {
  return { exitCode, stdout: json ? `${JSON.stringify(data, null, 2)}\n` : line(text), stderr: "" };
}

// The only error renderer: every command reports errors through it (spec §10.3).
export function failure(
  code: ErrorCode,
  message: string,
  json: boolean,
  more: { fix?: string; details?: string[]; exitCode?: ExitCode } = {},
): CommandResult {
  const fix = more.fix ?? ERRORS[code].fix;
  const details = more.details ?? [];
  const exitCode = more.exitCode ?? 2;
  if (json) {
    return { exitCode, stdout: `${JSON.stringify({ ok: false, error: { code, message, fix, details } }, null, 2)}\n`, stderr: "" };
  }
  const lines = [`${code} ${message}`, ...details.map((d) => `  ${d}`), `  fix: ${fix}`];
  return { exitCode, stdout: "", stderr: `${lines.join("\n")}\n` };
}

export function fromError(e: unknown, json: boolean): CommandResult {
  if (e instanceof SindriError) return failure(e.code, e.message, json, { fix: e.fix, details: e.details, exitCode: e.exitCode });
  throw e;
}
