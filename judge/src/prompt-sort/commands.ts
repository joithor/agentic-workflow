// judge/src/prompt-sort/commands.ts
// `judge prompt-sort <sub>` dispatcher. No subcommand = sort one prompt from
// stdin (the hook's path). Eval-side subcommands are added in Tasks 8-9.
import type { JudgeConfig } from "../config.js";
import { getDecision, getDecisionDetails, type Db } from "../db.js";
import type { JevDeps } from "../providers/jev-api.js";
import { runPromptSort, type CmdResult } from "./run.js";
import { runScaffoldSwitch } from "./scaffold-switch.js";
import { promptSortAxesFor, promptSortRunFor } from "./store.js";

export { runScaffoldSwitch } from "./scaffold-switch.js";

export interface PromptSortCliDeps {
  db: Db; config: JudgeConfig; configFile: string; stateDir: string; jev: JevDeps | null;
  readStdin: () => Promise<string>; now?: () => Date; randomId?: () => string; clock?: () => number;
}

export function runPromptSortWhy(db: Db, id: string): CmdResult {
  const decision = getDecision(db, id);
  if (decision === undefined || decision.question !== "prompt-sort") {
    return { exitCode: 1, stdout: "", stderr: `unknown prompt-sort decision: ${id}` };
  }
  return {
    exitCode: 0,
    stdout: JSON.stringify({ decision, details: getDecisionDetails(db, id), axes: promptSortAxesFor(db, id), run: promptSortRunFor(db, id) }),
  };
}

export async function runPromptSortCommand(args: string[], deps: PromptSortCliDeps): Promise<CmdResult> {
  const [sub, ...rest] = args;
  switch (sub) {
    case undefined: {
      let input: unknown;
      try {
        input = JSON.parse(await deps.readStdin());
      } catch {
        return { exitCode: 1, stdout: "", stderr: "input is not valid JSON" };
      }
      return runPromptSort(input, { db: deps.db, config: deps.config, jev: deps.jev, stateDir: deps.stateDir, now: deps.now, randomId: deps.randomId, clock: deps.clock });
    }
    case "scaffold":
      return runScaffoldSwitch(rest[0] ?? "", rest[1] ?? "", deps.configFile);
    case "why":
      return runPromptSortWhy(deps.db, rest[0] ?? "");
    default:
      return { exitCode: 1, stdout: "", stderr: `unknown prompt-sort subcommand: ${sub}` };
  }
}
