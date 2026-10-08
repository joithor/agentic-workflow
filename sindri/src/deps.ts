import path from "node:path";

import type { GitRunner } from "./git.js";
import type { SystemProbe } from "./system.js";

// Everything a command needs from the outside world. Commands never read
// process.env, process.cwd() or the clock directly, so tests pass a fake.
export interface Deps {
  env: NodeJS.ProcessEnv;
  cwd: string;
  home: string;
  now: () => Date;
  system: SystemProbe;
  git: GitRunner;
  isTTY: boolean;
  prompt: (question: string) => Promise<string>;
}

// $AW_STATE_DIR, default ~/.agentic-workflow (shared with judge and scorer).
export function awStateDir(deps: Deps): string {
  return deps.env.AW_STATE_DIR ?? path.join(deps.home, ".agentic-workflow");
}

export function stateDir(deps: Deps): string {
  return path.join(awStateDir(deps), "sindri");
}
