import { z } from "zod";

import { parseFlags } from "../../args.js";
import { awStateDir } from "../../deps.js";
import { SindriError } from "../../errors.js";
import { heavyLockState } from "../../index/heavy-lock.js";
import { success, type CommandResult, type ExitCode } from "../../output.js";
import { check, dirtyOf } from "./check.js";
import { correctCommand } from "./correct.js";
import { reflectCommand, reflectedBefore } from "./reflect.js";
import { stage } from "./stage.js";
import { telemetry } from "./telemetry.js";
import type { EvolveCtx } from "../ctx.js";
import { ghJson, ghRepoOf } from "../github.js";

const PrList = z.array(z.object({ number: z.number().int().positive() }));

const messageOf = (e: unknown): string => (e instanceof SindriError ? `${e.code} ${e.message}` : e instanceof Error ? e.message : String(e));

// The last line of a step's output that isn't a "Next:" pointer. Steps throw on error, so stdout is all there is.
function summaryOf(r: CommandResult): string {
  const lines = r.stdout.split("\n").filter((l) => l !== "" && !l.startsWith("Next:") && !l.startsWith("  fix:"));
  return lines[lines.length - 1];
}

async function mergedUnreflected(ctx: EvolveCtx): Promise<number[]> {
  const ghRepo = await ghRepoOf(ctx.deps.git, ctx.repo);
  const since = new Date(ctx.deps.now().getTime() - 7 * 86_400_000).toISOString().slice(0, 10);
  const list = await ghJson(ctx.io.process, ["gh", "pr", "list", "--state", "merged", "--search", `merged:>=${since}`, "--json", "number", "--limit", "100", "--repo", ghRepo], ctx.repo, PrList);
  return list.map((p) => p.number).filter((n) => !reflectedBefore(ctx, n).reflected).sort((a, b) => a - b);
}

const STATE: Record<number, "ok" | "attn" | "FAIL"> = { 0: "ok", 1: "attn", 2: "FAIL" };

async function reflectStep(ctx: EvolveCtx): Promise<CommandResult> {
  const todo = await mergedUnreflected(ctx);
  if (todo.length === 0) return success("no merged PRs from the last 7 days need a reflection", {}, false);
  const parts: string[] = [];
  let worst: ExitCode = 0;
  for (const n of todo) {
    let code: ExitCode;
    try {
      code = (await reflectCommand(["--pr", String(n)], ctx)).exitCode;
    } catch {
      code = 2;
    }
    parts.push(`#${n} ${STATE[code]}`);
    worst = Math.max(worst, code) as ExitCode;
  }
  return { exitCode: worst, stdout: `reflected on ${todo.length} merged PR(s): ${parts.join(", ")}`, stderr: "" };
}

async function checkStep(ctx: EvolveCtx): Promise<CommandResult> {
  if (await dirtyOf(ctx)) return success("skipped: the working tree has uncommitted changes, so suites wouldn't match a commit", {}, false, 1);
  return check(["--changed"], ctx);
}

export async function weekly(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { "dry-run": { type: "boolean" }, json: { type: "boolean" } });
  const json = values.json === true;
  if (heavyLockState(awStateDir(ctx.deps), ctx.deps.now, ctx.deps.env).held) {
    return success("Skipped: a heavy job holds the box-wide lock. Nothing ran.\nNext: rerun sindri evolve weekly later", { skipped: true }, json, 1);
  }
  if (values["dry-run"] === true) {
    let reflectPlan: string;
    let prs: number[] = [];
    try {
      prs = await mergedUnreflected(ctx);
      reflectPlan = `reflect on ${prs.length} merged PR(s)${prs.length > 0 ? ` (${prs.map((n) => `#${n}`).join(", ")})` : ""}`;
    } catch (e) {
      reflectPlan = `reflect: couldn't list merged PRs (${messageOf(e)})`;
    }
    return success(`Weekly plan (dry run): telemetry --since 7d; ${reflectPlan}; correct --since 7d; check --changed; stage. Nothing ran.\nNext: sindri evolve weekly`, { dryRun: true, prs }, json);
  }
  const steps: { name: string; run: () => Promise<CommandResult> }[] = [
    { name: "telemetry", run: () => telemetry(["--since", "7d"], ctx) },
    { name: "reflect", run: () => reflectStep(ctx) },
    { name: "correct", run: () => correctCommand(["--since", "7d"], ctx) },
    { name: "check", run: () => checkStep(ctx) },
    { name: "stage", run: () => stage([], ctx) },
  ];
  const results: { name: string; state: "ok" | "attn" | "FAIL"; line: string }[] = [];
  for (const s of steps) {
    try {
      const r = await s.run();
      results.push({ name: s.name, state: STATE[r.exitCode], line: summaryOf(r) });
    } catch (e) {
      results.push({ name: s.name, state: "FAIL", line: messageOf(e) });
    }
  }
  const ok = results.filter((r) => r.state === "ok").length;
  const staged = (ctx.db.prepare("SELECT COUNT(*) AS c FROM proposals WHERE status = 'staged'").get() as { c: number }).c;
  const next = ok < results.length ? "fix the lines above, then rerun sindri evolve weekly (or just the failing step)" : staged > 0 ? "sindri evolve publish" : "sindri evolve proposals";
  const lines = [...results.map((r) => `${r.state.padEnd(4)} ${r.name}: ${r.line}`), `Weekly: ${results.length} steps, ${ok} ok, ${results.length - ok} need attention.`, `Next: ${next}`];
  return success(lines.join("\n"), { steps: results, ok, staged }, json, ok === results.length ? 0 : 1);
}
