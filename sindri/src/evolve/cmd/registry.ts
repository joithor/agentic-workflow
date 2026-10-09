import { parseFlags } from "../../args.js";
import { success, type CommandResult } from "../../output.js";
import { repoConfig, type EvolveCtx } from "../ctx.js";
import { discover, saveRegistry } from "../registry.js";

export async function init(args: string[], ctx: EvolveCtx): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const artifacts = await discover(ctx.deps.git, ctx.repo, ctx.prompts(), repoConfig(ctx.loaded).protectedPaths);
  const counts = ctx.write((epoch) => saveRegistry(ctx.db, artifacts, epoch, ctx.deps.now()));
  const kinds = [...new Set(artifacts.map((a) => a.kind))].sort().map((k) => `${artifacts.filter((a) => a.kind === k).length} ${k}`).join(", ");
  const protectedCount = artifacts.filter((a) => a.protected).length;
  const noSuite = artifacts.filter((a) => a.suite === null).length;
  const text = [
    `Registry: ${artifacts.length} artifacts${kinds === "" ? "" : ` (${kinds})`}; ${counts.added} added, ${counts.changed} changed, ${counts.removed} removed; ${protectedCount} protected, ${noSuite} without a suite.`,
    `Next: ${artifacts.length === 0 ? "sindri evolve status" : "sindri evolve check --changed"}`,
  ].join("\n");
  return success(text, { counts, artifacts: artifacts.map((a) => ({ id: a.id, kind: a.kind, protected: a.protected, hasSuite: a.suite !== null })) }, values.json === true);
}
