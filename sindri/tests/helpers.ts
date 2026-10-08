import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import type { Deps } from "../src/deps.js";

export function tempDir(prefix = "sindri-test-"): string {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}

// A Deps bag that never touches the real ~/.agentic-workflow: AW_STATE_DIR
// points at a fresh temp dir. Later tasks add fields here as Deps grows.
export function makeDeps(overrides: Partial<Deps> = {}): Deps {
  const home = tempDir("sindri-home-");
  return {
    env: { AW_STATE_DIR: path.join(home, ".agentic-workflow") },
    cwd: home,
    home,
    now: () => new Date("2026-10-08T12:00:00.000Z"),
    ...overrides,
  };
}
