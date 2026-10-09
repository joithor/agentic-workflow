import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import type { Deps } from "../src/deps.js";
import type { GitResult, GitRunner } from "../src/git.js";
import { realGitRunner } from "../src/git-real.js";
import type { IndexIo } from "../src/index/io.js";
import type { SystemProbe } from "../src/system.js";

export function tempDir(prefix = "sindri-test-"): string {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}

// An IndexIo that behaves like a machine with nothing installed: Ollama refuses, no binaries.
export function fakeIndexIo(over: Partial<IndexIo> = {}): IndexIo {
  return {
    fetch: async () => {
      throw new Error("connect ECONNREFUSED");
    },
    probes: { has: () => false, run: async () => ({ code: 127, stdout: "", stderr: "not found" }), getJson: async () => null },
    ...over,
  };
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
    io: fakeIndexIo(),
    isTTY: false,
    prompt: async () => "",
    stdin: async () => "",
    sleep: async () => undefined,
    log: () => undefined,
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

export const COMMIT_DATE = "2026-10-08T12:00:00+00:00";

// git with a fixed author and commit date: tree hashes and `git log --since` stay deterministic.
export function git(cwd: string, ...args: string[]): string {
  return execFileSync("git", ["-c", "user.name=Tester", "-c", "user.email=tester@example.com", ...args], {
    cwd,
    encoding: "utf8",
    env: { ...process.env, GIT_AUTHOR_DATE: COMMIT_DATE, GIT_COMMITTER_DATE: COMMIT_DATE },
  });
}

export function gitRepo(files: Record<string, string>): string {
  const root = tempDir("sindri-repo-");
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  git(root, "init", "-q", "-b", "main");
  git(root, "add", "-A");
  git(root, "commit", "-qm", "init", "--allow-empty");
  return root;
}
