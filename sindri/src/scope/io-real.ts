import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { realProcessRunner } from "../index/sandbox-real.js";
import type { ScopeIo } from "./commands.js";
import { makeClaudeRunner } from "./model.js";
import { realSpawner } from "./model-real.js";

// The real adapter (coverage-excluded, like model-real.ts): the Claude CLI with the
// profile's effort and base-URL setting, scrubbing every prompt with the profile's
// scrubber; a scratch dir per call, removed afterwards; progress lines on stderr.
export function realScopeIo(): ScopeIo {
  return {
    runner: (loaded, scrubber) =>
      makeClaudeRunner({
        spawn: realSpawner(),
        providers: loaded.profile.providers.allowed,
        scrubber,
        makeDir: () => fs.mkdtempSync(path.join(os.tmpdir(), "sindri-scope-")),
        removeDir: (dir) => fs.rmSync(dir, { recursive: true, force: true }),
        env: process.env,
        effort: loaded.profile.models.effort,
        allowBaseUrl: loaded.profile.models.allowBaseUrl,
      }),
    fetch: (url, init) => fetch(url, init),
    process: realProcessRunner(),
    progress: (line) => {
      process.stderr.write(`${line}\n`);
    },
  };
}
