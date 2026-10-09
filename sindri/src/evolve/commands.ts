import { stateDir } from "../deps.js";
import { ledgerPath, openLedger } from "../ledger/db.js";
import type { Command } from "../main.js";
import { failure, fromError } from "../output.js";
import { requireApprovedProfile } from "../profile/approve.js";
import { adopt, revert } from "./cmd/adopt.js";
import { check } from "./cmd/check.js";
import { compare } from "./cmd/compare.js";
import { correctCommand } from "./cmd/correct.js";
import { proposals, reject, show, tier } from "./cmd/proposals.js";
import { reflectCommand } from "./cmd/reflect.js";
import { init } from "./cmd/registry.js";
import { publish, stage } from "./cmd/stage.js";
import { status } from "./cmd/status.js";
import { telemetry } from "./cmd/telemetry.js";
import { ringZeroRepo, writers, type EvolveIo, type Sub } from "./ctx.js";
import { effectivePrompts } from "./overlay.js";

// Later tasks add their subcommands here.
export const SUBCOMMANDS: Record<string, Sub> = { adopt, check, compare, correct: correctCommand, init, proposals, reflect: reflectCommand, publish, reject, revert, show, stage, status, telemetry, tier };

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
        return await SUBCOMMANDS[sub](rest, { deps, io, loaded, db, repo: ringZeroRepo(loaded), prompts: () => effectivePrompts(deps), ...writers(deps, db) });
      } finally {
        db.close();
      }
    } catch (e) {
      return fromError(e, json);
    }
  };
}
