import { formatFindings, runTraitCheck, type TraitCheckDeps } from "./check.js";

const USAGE = "usage: judge trait-check --edit --file <path> [--file <path>...] | --stop [--base <ref>]  [--budget-ms <n>] [--min-p <0..1>]";

// Defaults per mode: the edit nudge only informs, so it reports earlier; the Stop
// gate holds the agent back, so it needs a surer call. Budgets sit under the hook
// timeouts in scripts/install-trait-check.sh.
const DEFAULTS = { edit: { budgetMs: 15_000, minP: 0.6 }, stop: { budgetMs: 40_000, minP: 0.8 } } as const;

/**
 * Exit codes: 0 nothing to report (or nothing to check), 2 findings on stderr,
 * 3 Jev unavailable (the hook logs it as a fail-open), 64 usage.
 */
export async function runTraitCheckCli(args: readonly string[], deps: TraitCheckDeps): Promise<{ exitCode: number; stdout: string; stderr?: string }> {
  const files: string[] = [];
  let mode: "edit" | "stop" | undefined;
  let base = "origin/main";
  let budgetMs: number | undefined;
  let minP: number | undefined;
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    const value = args[i + 1];
    if (arg === "--edit" || arg === "--stop") {
      if (mode !== undefined) return { exitCode: 64, stdout: "", stderr: USAGE };
      mode = arg === "--edit" ? "edit" : "stop";
    } else if (value !== undefined && (arg === "--file" || arg === "--base" || arg === "--budget-ms" || arg === "--min-p")) {
      i++;
      if (arg === "--file") files.push(value);
      else if (arg === "--base") base = value;
      else if (arg === "--budget-ms") budgetMs = Number(value);
      else minP = Number(value);
    } else return { exitCode: 64, stdout: "", stderr: USAGE };
  }
  if (mode === undefined || (mode === "edit") !== files.length > 0) return { exitCode: 64, stdout: "", stderr: USAGE };
  budgetMs ??= DEFAULTS[mode].budgetMs;
  minP ??= DEFAULTS[mode].minP;
  if (!Number.isInteger(budgetMs) || budgetMs <= 0 || !(minP >= 0 && minP <= 1)) return { exitCode: 64, stdout: "", stderr: USAGE };

  const result = await runTraitCheck(deps, { mode, files, base, budgetMs, minP });
  if (result.status === "skipped") return { exitCode: 0, stdout: "" };
  if (result.status === "unavailable") return { exitCode: 3, stdout: "", stderr: `trait-check: jev unavailable (${result.reason})` };
  if (result.findings.length === 0) return { exitCode: 0, stdout: "" };
  return { exitCode: 2, stdout: "", stderr: formatFindings(mode, result.findings, result.root) };
}
