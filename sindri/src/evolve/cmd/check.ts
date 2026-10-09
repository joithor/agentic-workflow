import { parseFlags } from "../../args.js";
import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import type { EvolveCtx } from "../ctx.js";
import { loadRegistry, type Artifact } from "../registry.js";
import { runSuite } from "../suites.js";

export type WithSuite = Artifact & { suite: NonNullable<Artifact["suite"]> };
const hasSuite = (a: Artifact): a is WithSuite => a.suite !== null;
const suiteKey = (a: WithSuite): string => JSON.stringify([a.suite.argv, a.suite.cwd]);

const passing = (ctx: EvolveCtx, a: Artifact): boolean =>
  ctx.db.prepare("SELECT 1 FROM suite_runs WHERE artifact_id = ? AND hash = ? AND ok = 1 LIMIT 1").get(a.id, a.hash) !== undefined;

export async function headOf(ctx: EvolveCtx): Promise<string | null> {
  const r = await ctx.deps.git.run(["rev-parse", "HEAD"], ctx.repo);
  return r.ok ? r.stdout.trim() : null;
}

export async function dirtyOf(ctx: EvolveCtx): Promise<boolean> {
  const r = await ctx.deps.git.run(["status", "--porcelain"], ctx.repo);
  return r.ok && r.stdout.trim() !== "";
}

export function groupBySuite(list: WithSuite[]): WithSuite[][] {
  const groups = new Map<string, WithSuite[]>();
  for (const a of list) groups.set(suiteKey(a), [...(groups.get(suiteKey(a)) ?? []), a]);
  return [...groups.values()];
}

export async function check(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values, positionals } = parseFlags(args, { changed: { type: "boolean" }, list: { type: "boolean" }, json: { type: "boolean" } });
  const json = values.json === true;
  const registry = loadRegistry(ctx.db);
  if (registry.length === 0) throw new SindriError("SND-EVOLVE-010", "the artifact registry is empty");
  const unknown = positionals.filter((id) => !registry.some((a) => a.id === id));
  if (unknown.length > 0) throw new SindriError("SND-EVOLVE-008", `no such artifact: ${unknown.join(", ")}`);
  const picked = positionals.length > 0 ? registry.filter((a) => positionals.includes(a.id)) : registry;
  const groups = groupBySuite(picked.filter(hasSuite).filter((a) => values.changed !== true || !passing(ctx, a)));

  if (groups.length === 0) {
    const text = values.changed === true && picked.some(hasSuite)
      ? "All suites already pass for the current files. Nothing to run."
      : picked.every((a) => a.suite === null) && positionals.length > 0
        ? `${picked.map((a) => a.id).join(", ")}: no suite`
        : "No artifact has a suite to run.";
    return success(`${text}\nNext: sindri evolve status`, { groups: [] }, json);
  }

  if (values.list === true) {
    const lines = groups.map((g) => `would run ${g[0].suite.argv.join(" ")} (in ${g[0].suite.cwd}) for ${g.map((a) => a.id).join(", ")}`);
    return success([...lines, `${groups.length} suite(s) would run.`, "Next: sindri evolve check --changed"].join("\n"), { groups: groups.map((g) => ({ artifacts: g.map((a) => a.id), argv: g[0].suite.argv, cwd: g[0].suite.cwd })) }, json);
  }

  const head = await headOf(ctx);
  const dirty = await dirtyOf(ctx);
  const results: { artifacts: string[]; ok: boolean; exitCode: number; ms: number; tail: string }[] = [];
  const lines: string[] = [];
  for (const [i, g] of groups.entries()) {
    const ids = g.map((a) => a.id);
    ctx.deps.log(`running ${ids.join(", ")} (${i + 1} of ${groups.length})`);
    const r = await runSuite(ctx.deps, ctx.io.process, ctx.repo, g[0]);
    await ctx.writeRetry((epoch) => {
      for (const a of g) {
        ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)")
          .run(a.id, a.hash, head, dirty ? 1 : 0, r.ok ? 1 : 0, r.exitCode, r.ms, ctx.deps.now().toISOString(), epoch);
      }
    });
    results.push({ artifacts: ids, ok: r.ok, exitCode: r.exitCode, ms: r.ms, tail: r.tail });
    if (r.ok) lines.push(`ok   ${ids.join(", ")} (${(r.ms / 1000).toFixed(1)} s)`);
    else lines.push(`FAIL ${ids.join(", ")} (exit ${r.exitCode})`, ...r.tail.split("\n").map((l) => `    ${l}`));
  }
  const failed = results.filter((r) => !r.ok);
  const summary = `Checked ${results.length} suite(s): ${results.length - failed.length} ok, ${failed.length} FAILED.`;
  const next = failed.length > 0 ? `fix the failing suite, then: sindri evolve check ${failed[0].artifacts[0]}` : "sindri evolve status";
  const text = [...lines, summary, ...(dirty ? ["Note: the working tree has uncommitted changes; results are bound to the file hashes, not to HEAD."] : []), `Next: ${next}`].join("\n");
  return success(text, { head, dirty, groups: results.map(({ artifacts, ok, exitCode, ms }) => ({ artifacts, ok, exitCode, ms })) }, json, failed.length > 0 ? 1 : 0);
}

