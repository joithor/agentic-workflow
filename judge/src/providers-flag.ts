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

// The allowlist gates every provider call, not just the evaluate chain:
// prompt-sort (jev + its adjudicator) and adjudicate (claude-cli) call
// providers directly. null means no allowlist, so everything is allowed.
export function isProviderAllowed(allowed: readonly ProviderName[] | null, name: ProviderName): boolean {
  return allowed === null || allowed.includes(name);
}

// prompt-sort: a provider outside the allowlist is dropped (jev -> its rules
// path; adjudicator -> "needs the claude CLI" refusal).
export function gatePromptSortDeps<J, A>(allowed: readonly ProviderName[] | null, jev: J | null, adjudicator: A | null): { jev: J | null; adjudicator: A | null } {
  return {
    jev: isProviderAllowed(allowed, "jev") ? jev : null,
    adjudicator: isProviderAllowed(allowed, "claude-cli") ? adjudicator : null,
  };
}

// adjudicate always uses claude-cli: refuse (usage error, exit 64) when excluded.
export function adjudicateRefusal(allowed: readonly ProviderName[] | null): { exitCode: number; stdout: string; stderr: string } | null {
  if (isProviderAllowed(allowed, "claude-cli")) return null;
  return { exitCode: 64, stdout: "", stderr: "judge: adjudicate uses claude-cli, which is not in --providers / AW_JUDGE_PROVIDERS" };
}
