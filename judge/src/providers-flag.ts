import { isProviderName } from "./config.js";
import type { ProviderName } from "./types.js";

export type AllowlistResult = { ok: true; allowed: ProviderName[] | null; argv: string[] } | { ok: false; error: string };

// Global flag, accepted anywhere in argv so it composes with every subcommand:
//   judge --providers jev,claude-cli <cmd> ...
// AW_JUDGE_PROVIDERS is the env equivalent (hooks can't always add flags).
export function parseProvidersAllowlist(argv: readonly string[], env: NodeJS.ProcessEnv): AllowlistResult {
  const i = argv.indexOf("--providers");
  let raw: string | undefined;
  let rest = [...argv];
  if (i !== -1) {
    raw = argv[i + 1];
    if (raw === undefined) return { ok: false, error: "--providers needs a comma-separated list" };
    rest = [...argv.slice(0, i), ...argv.slice(i + 2)];
  } else if (env.AW_JUDGE_PROVIDERS !== undefined) {
    raw = env.AW_JUDGE_PROVIDERS;
  } else {
    return { ok: true, allowed: null, argv: rest };
  }
  const names = raw.split(",").map((s) => s.trim()).filter((s) => s.length > 0);
  if (names.length === 0) return { ok: false, error: "--providers list is empty" };
  const bad = names.filter((n) => !isProviderName(n));
  if (bad.length > 0) return { ok: false, error: `unknown provider(s): ${bad.join(", ")}` };
  return { ok: true, allowed: [...new Set(names as ProviderName[])], argv: rest };
}
