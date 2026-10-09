import path from "node:path";

import { parseFlags } from "../../args.js";
import { stateDir, type Deps } from "../../deps.js";
import { SindriError } from "../../errors.js";
import type { ProcessRunner } from "../../index/io.js";
import { ledgerPath, openLedger, type Ledger } from "../../ledger/db.js";
import type { Command } from "../../main.js";
import { failure, fromError, success, type CommandResult } from "../../output.js";
import { requireApprovedProfile } from "../../profile/approve.js";
import type { LoadedProfile } from "../../profile/load.js";
import { audit } from "../audit.js";
import { canPromote, promote, readChannels, rollback, wrapperTarget } from "../channel.js";
import { repoConfig, ringZeroRepo, withLockedWriteRetry } from "../ctx.js";
import { isProtectedPath } from "../registry.js";

export interface ChannelIo {
  process: ProcessRunner;
}

interface ChannelCtx {
  deps: Deps;
  db: Ledger;
  loaded: LoadedProfile;
  repo: string;
  process: ProcessRunner;
}
type ChannelSub = (args: string[], ctx: ChannelCtx) => Promise<CommandResult>;

const short = (sha: string): string => sha.slice(0, 8);
const suiteRunAt = (db: Ledger, sha: string): boolean =>
  db.prepare("SELECT 1 FROM suite_runs WHERE artifact_id = 'package:sindri' AND ok = 1 AND head = ? AND hash = ? AND dirty = 0 LIMIT 1").get(sha, `at:${sha}`) !== undefined;

const status: ChannelSub = async (args, ctx) => {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const json = values.json === true;
  const c = readChannels(ctx.deps);
  if (c.stable === null && c.next === null) return success("No channels recorded.\nNext: scripts/install-sindri.sh --channel next --ref <sha>", { stable: null, next: null }, json);
  const can = canPromote(c, c.next?.sha ?? "", c.next !== null && suiteRunAt(ctx.db, c.next.sha), ctx.deps.now());
  const cli = wrapperTarget(ctx.deps);
  const expected = c.stable === null ? null : path.join(c.stable.dir, "dist", "cli.js");
  const mismatch = cli !== null && expected !== null && cli !== expected;
  const wrapper = cli === null ? "wrapper: none found" : mismatch ? `wrapper: runs ${cli} (does not match stable; an in-place install may have overwritten it)` : `wrapper: runs ${cli}`;
  const lines = [
    c.stable === null ? "stable none" : `stable ${short(c.stable.sha)} (since ${c.stable.installedAt.slice(0, 10)}); previous ${c.stable.previous === null ? "none" : short(c.stable.previous.sha)}`,
    c.next === null ? "next   none" : `next   ${short(c.next.sha)} (since ${c.next.installedAt.slice(0, 10)}); ${can.why}`,
    wrapper,
    `Next: ${can.next}`,
  ];
  return success(lines.join("\n"), { ...c, canPromote: can, wrapper: cli, wrapperMismatch: mismatch }, json, mismatch ? 1 : 0);
};

async function confirmed(ctx: ChannelCtx, text: string, sha: string): Promise<void> {
  if (!ctx.deps.isTTY) throw new SindriError("SND-EVOLVE-006", "changing a channel needs an interactive terminal");
  const answer = await ctx.deps.prompt(`${text}\nType the first 8 characters of the sha to confirm: `);
  if (answer.trim() !== short(sha)) throw new SindriError("SND-EVOLVE-007", "the confirmation didn't match");
}

const promoteSub: ChannelSub = async (args, ctx) => {
  const { values, positionals } = parseFlags(args, { json: { type: "boolean" } });
  const sha = positionals[0];
  if (sha === undefined || !/^[0-9a-f]{40}$/.test(sha)) throw new SindriError("SND-CLI-002", "usage: sindri channel promote <40-character sha>");
  const c = readChannels(ctx.deps);
  const can = canPromote(c, sha, suiteRunAt(ctx.db, sha), ctx.deps.now());
  if (!can.ok) throw new SindriError("SND-EVOLVE-005", can.why, { fix: can.next });
  let note = "This is the first stable build.";
  if (c.stable !== null) {
    const d = await ctx.deps.git.run(["diff", "--name-only", `${c.stable.sha}..${sha}`], ctx.repo);
    const hit = d.ok ? d.stdout.split("\n").filter((f) => f !== "" && isProtectedPath(f, repoConfig(ctx.loaded).protectedPaths)) : null;
    note = hit === null ? "Couldn't list the changes since stable." : hit.length > 0 ? `Protected paths changed since stable: ${hit.join(", ")}` : "No protected paths changed since stable.";
  }
  await confirmed(ctx, `Promote ${short(sha)} to stable?\n${note}`, sha);
  const before = c.stable?.sha;
  await promote(ctx.deps, ctx.process, sha, ctx.deps.now());
  await withLockedWriteRetry(ctx.deps, ctx.db, (epoch) => audit(ctx.db, ctx.deps, "promote", `${sha} (previous ${before ?? "none"})`, epoch));
  return success(`Promoted ${short(sha)} to stable. Roll back with: sindri channel rollback\nNext: sindri channel status`, { promoted: sha, previous: before ?? null }, values.json === true);
};

const rollbackSub: ChannelSub = async (args, ctx) => {
  const { values } = parseFlags(args, { json: { type: "boolean" } });
  const prev = readChannels(ctx.deps).stable?.previous ?? null;
  if (prev === null) throw new SindriError("SND-EVOLVE-005", "there is no previous stable build to roll back to");
  await confirmed(ctx, `Roll stable back to ${short(prev.sha)}?`, prev.sha);
  await rollback(ctx.deps, ctx.process, ctx.deps.now());
  await withLockedWriteRetry(ctx.deps, ctx.db, (epoch) => audit(ctx.db, ctx.deps, "rollback", `to ${prev.sha}`, epoch));
  return success(`Rolled back to ${short(prev.sha)}. There is no previous build now; to go forward, promote a build from next.\nNext: sindri channel status`, { rolledBackTo: prev.sha }, values.json === true);
};

const SUBS: Record<string, ChannelSub> = { status, promote: promoteSub, rollback: rollbackSub };

export function makeChannelCommand(io: ChannelIo): Command {
  return async (args, deps) => {
    const [sub, ...rest] = args;
    const json = rest.includes("--json");
    if (sub === undefined || !Object.hasOwn(SUBS, sub)) {
      return failure("SND-CLI-002", `unknown channel subcommand: ${sub ?? "(none)"}; use status, promote, rollback`, json, { fix: "sindri help" });
    }
    try {
      const db = openLedger(ledgerPath(stateDir(deps)));
      try {
        const loaded = requireApprovedProfile(deps, db);
        return await SUBS[sub](rest, { deps, db, loaded, repo: ringZeroRepo(loaded), process: io.process });
      } finally {
        db.close();
      }
    } catch (e) {
      return fromError(e, json);
    }
  };
}
