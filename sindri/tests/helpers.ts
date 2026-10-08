import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import type { Deps } from "../src/deps.js";
import type { GitResult, GitRunner } from "../src/git.js";
import { realGitRunner } from "../src/git-real.js";
import type { SystemProbe } from "../src/system.js";

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
    system: fakeSystem(),
    git: realGitRunner(),
    isTTY: false,
    prompt: async () => "",
    ...overrides,
  };
}

export function fakeSystem(over: Partial<SystemProbe> = {}): SystemProbe {
  return {
    platform: "darwin",
    pid: 4242,
    hostname: () => "test-host",
    bootId: () => "boot-1",
    pidAlive: () => true,
    pidStartTime: (pid) => `start-${pid}`,
    isLocalDisk: () => true,
    username: () => "tester",
    ...over,
  };
}

export function fakeGit(answers: Record<string, GitResult>): GitRunner {
  return {
    run: async (args) => answers[args.join(" ")] ?? { ok: false, stderr: `unexpected git call: ${args.join(" ")}` },
  };
}
