// judge/src/prompt-sort/commands.ts
// `judge prompt-sort <sub>` dispatcher. No subcommand = sort one prompt from
// stdin (the hook's path). Eval-side subcommands: import, outcomes, adjudicate.
import fs from "node:fs";

import type { JudgeConfig } from "../config.js";
import { getDecision, getDecisionDetails, type Db } from "../db.js";
import type { JevDeps } from "../providers/jev-api.js";
import type { Provider } from "../types.js";
import { adjudicateSort } from "./adjudicate.js";
import { importSortItems } from "./import.js";
import { runSortOutcomeLabels } from "./outcomes.js";
import { runPromptSort, type CmdResult } from "./run.js";
import { runScaffoldSwitch } from "./scaffold-switch.js";
import { promptSortAxesFor, promptSortRunFor } from "./store.js";

export { runScaffoldSwitch } from "./scaffold-switch.js";

export interface PromptSortCliDeps {
  db: Db; config: JudgeConfig; configFile: string; stateDir: string; jev: JevDeps | null;
  projectsDir: string; adjudicator: (() => Provider) | null;
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
    case "import": {
      const days = Number(/--since\s+(\d+)d/.exec(rest.join(" "))?.[1] ?? "14");
      const sinceIso = new Date((deps.now?.() ?? new Date()).getTime() - days * 86400000).toISOString();
      return { exitCode: 0, stdout: JSON.stringify(importSortItems(deps.db, { sinceIso })) };
    }
    case "outcomes": {
      if (!fs.existsSync(deps.projectsDir)) return { exitCode: 1, stdout: "", stderr: `no transcripts at ${deps.projectsDir}` };
      return { exitCode: 0, stdout: JSON.stringify(runSortOutcomeLabels(deps.db, { projectsDir: deps.projectsDir, now: deps.now ?? (() => new Date()) })) };
    }
    case "adjudicate": {
      if (deps.adjudicator === null) return { exitCode: 1, stdout: "", stderr: "claude CLI not found on PATH (the adjudicator needs it)" };
      const limit = Number(/--limit\s+(\d+)/.exec(rest.join(" "))?.[1] ?? "80");
      return { exitCode: 0, stdout: JSON.stringify(await adjudicateSort(deps.db, { provider: deps.adjudicator(), limit, now: deps.now ?? (() => new Date()) })) };
    }
    case "why":
      return runPromptSortWhy(deps.db, rest[0] ?? "");
    default:
      return { exitCode: 1, stdout: "", stderr: `unknown prompt-sort subcommand: ${sub}` };
  }
}
