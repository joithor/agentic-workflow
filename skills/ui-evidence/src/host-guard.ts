// Guard rails for running against a host other than localhost:3000. Pure
// functions: the CLI and the browser drivers call them before anything
// touches the network.
import type { ScriptStep } from "./script-schema.js";

export const DEFAULT_HOST = "http://localhost:3000";
// Only per-PR living previews: pr-<number>.vitalize.build. dev/staging/prod
// hosts under the same domain are deliberately not matched.
const PREVIEW_HOST = /^pr-\d+\.vitalize\.build$/;

export type HostResult = { ok: true; host: string; kind: "local" | "preview" } | { ok: false; error: string };

export interface HostFlags {
  host?: string;
  allowPreviewHost?: boolean;
}

export function resolveHost(flags: HostFlags): HostResult {
  if (flags.host === undefined) return { ok: true, host: DEFAULT_HOST, kind: "local" };
  let url: URL;
  try {
    url = new URL(flags.host);
  } catch {
    return { ok: false, error: `--host is not a URL: ${flags.host}` };
  }
  if (url.protocol !== "https:") return { ok: false, error: "--host must be https" };
  if (url.username !== "" || url.password !== "") return { ok: false, error: "--host must not carry credentials" };
  if (!PREVIEW_HOST.test(url.hostname)) return { ok: false, error: `--host ${url.hostname} is not a pr-<n>.vitalize.build preview host; dev and prod hosts are refused` };
  if (url.port !== "" || (url.pathname !== "/" && url.pathname !== "") || url.search !== "" || url.hash !== "") return { ok: false, error: "--host must be a bare origin (no port, path, query or fragment)" };
  if (flags.allowPreviewHost !== true) return { ok: false, error: "--host needs --allow-preview-host" };
  return { ok: true, host: url.origin, kind: "preview" };
}

/** Credentials come only from the environment; the error names variables, never values. */
export function readCredentials(env: Record<string, string | undefined>, emailEnv: string, passwordEnv: string): { email: string; password: string } | { error: string } {
  const email = env[emailEnv];
  const password = env[passwordEnv];
  const missing = [email ? null : emailEnv, password ? null : passwordEnv].filter((n): n is string => n !== null);
  return missing.length > 0 || !email || !password ? { error: `missing credentials in environment: ${missing.join(", ")}` } : { email, password };
}

/** Replaces every secret in `text`, so a Playwright error that echoes a filled value never reaches a log. */
export function redact(text: string, secrets: string[]): string {
  return secrets.filter((s) => s.length > 0).reduce((acc, s) => acc.split(s).join("[redacted]"), text);
}

const WRITE_WORDS = new Set(["save", "submit", "update", "delete", "remove", "create", "confirm", "publish", "send", "apply", "approve", "post", "add"]);

/** Splits camelCase and separators into lower-case words: "updateHospital-btn" -> update, hospital, btn. */
function words(target: string): string[] {
  return target.replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase().split(/[^a-z0-9]+/).filter((w) => w.length > 0);
}

/** Click steps whose target reads like a save/submit/update control. */
export function findWriteSteps(steps: ScriptStep[]): Array<{ index: number; target: string }> {
  return steps.flatMap((s, index) => (s.action === "click" && words(s.target).some((w) => WRITE_WORDS.has(w)) ? [{ index, target: s.target }] : []));
}

export function guardWrites(steps: ScriptStep[], allowWrites: boolean): { ok: true } | { ok: false; error: string } {
  const writes = allowWrites ? [] : findWriteSteps(steps);
  return writes.length === 0 ? { ok: true } : { ok: false, error: `refusing write-looking steps without --allow-writes: ${writes.map((w) => `#${w.index + 1} click ${w.target}`).join(", ")}` };
}

export type Provenance = "seeded" | "scrubbed" | "unknown";

/** A preview is a scrubbed customer extract; a local run is only as safe as its DB check says. */
export function provenanceFor(kind: "local" | "preview", dbProvenance: "seeded" | "unknown"): Provenance {
  return kind === "preview" ? "scrubbed" : dbProvenance;
}
