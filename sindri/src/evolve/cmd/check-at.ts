import { SindriError } from "../../errors.js";
import { success, type CommandResult } from "../../output.js";
import { buildProblem, readChannels } from "../channel.js";
import type { EvolveCtx } from "../ctx.js";
import { loadRegistry } from "../registry.js";
import { runSuite } from "../suites.js";

// The suite, run inside a channel build, bound to that sha: what `channel promote` requires.
export async function checkAt(sha: string, ids: string[], ctx: EvolveCtx, json: boolean): Promise<CommandResult> {
  if (!/^[0-9a-f]{40}$/.test(sha)) throw new SindriError("SND-CLI-002", "--at must be a full 40-character commit sha");
  if (ids.length > 1 || (ids.length === 1 && ids[0] !== "package:sindri")) throw new SindriError("SND-CLI-002", "--at runs package:sindri only: sindri evolve check package:sindri --at <sha>");
  const c = readChannels(ctx.deps);
  const entry = [c.next, c.stable].flatMap((e) => (e === null ? [] : [e])).find((e) => e.sha === sha);
  if (entry === undefined) throw new SindriError("SND-EVOLVE-005", `no channel build for ${sha}`, { fix: `scripts/install-sindri.sh --channel next --ref ${sha}` });
  const problem = buildProblem(ctx.deps, entry);
  if (problem !== null) throw new SindriError("SND-EVOLVE-005", problem);
  const artifact = loadRegistry(ctx.db).find((a) => a.id === "package:sindri");
  if (artifact === undefined || artifact.suite === null) throw new SindriError("SND-EVOLVE-008", "package:sindri has no suite; run sindri evolve init");
  ctx.deps.log(`running package:sindri at ${sha.slice(0, 8)}`);
  const r = await runSuite(ctx.deps, ctx.io.process, entry.dir, { id: artifact.id, suite: { argv: artifact.suite.argv, cwd: "." } });
  await ctx.writeRetry((epoch) => {
    ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES (?, ?, ?, 0, ?, ?, ?, ?, ?)")
      .run(artifact.id, `at:${sha}`, sha, r.ok ? 1 : 0, r.exitCode, r.ms, ctx.deps.now().toISOString(), epoch);
  });
  const head = `package:sindri at ${sha.slice(0, 8)}`;
  const text = r.ok
    ? `ok   ${head} (${(r.ms / 1000).toFixed(1)} s)\nNext: sindri channel promote ${sha}`
    : [`FAIL ${head} (exit ${r.exitCode})`, ...r.tail.split("\n").map((l) => `    ${l}`), `Next: fix the failing suite, then: sindri evolve check package:sindri --at ${sha}`].join("\n");
  return success(text, { sha, ok: r.ok, exitCode: r.exitCode, ms: r.ms }, json, r.ok ? 0 : 1);
}
