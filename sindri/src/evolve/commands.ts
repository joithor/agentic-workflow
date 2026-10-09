import { stateDir } from "../deps.js";
import { ledgerPath, openLedger } from "../ledger/db.js";
import type { Command } from "../main.js";
import { failure, fromError } from "../output.js";
import { requireApprovedProfile } from "../profile/approve.js";
import { check } from "./cmd/check.js";
import { init } from "./cmd/registry.js";
import { status } from "./cmd/status.js";
import { ringZeroRepo, writers, type EvolveIo, type Sub } from "./ctx.js";

// Later tasks add their subcommands here.
export const SUBCOMMANDS: Record<string, Sub> = { check, init, status };

export function evolveUsage(): string {
  return `Usage: sindri evolve ${Object.keys(SUBCOMMANDS).sort().join(" | ")}   (each takes --json)`;
}

export function makeEvolveCommand(io: EvolveIo): Command {
  return async (args, deps) => {
    const [sub, ...rest] = args;
    const json = rest.includes("--json");
    if (sub === undefined || !Object.hasOwn(SUBCOMMANDS, sub)) {
      return failure("SND-CLI-002", `unknown evolve subcommand: ${sub ?? "(none)"}; use ${Object.keys(SUBCOMMANDS).sort().join(", ")}`, json, { fix: "sindri help" });
    }
    try {
      const db = openLedger(ledgerPath(stateDir(deps)));
      try {
        const loaded = requireApprovedProfile(deps, db);
        // Task 5 replaces `prompts: () => []` with the effective prompt texts.
        return await SUBCOMMANDS[sub](rest, { deps, io, loaded, db, repo: ringZeroRepo(loaded), prompts: () => [], ...writers(deps, db) });
      } finally {
        db.close();
      }
    } catch (e) {
      return fromError(e, json);
    }
  };
}
