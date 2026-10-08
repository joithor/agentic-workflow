import { execFile } from "node:child_process";

import type { GitResult, GitRunner } from "./git.js";

export function realGitRunner(): GitRunner {
  return {
    run: (args, cwd) =>
      new Promise<GitResult>((resolve) => {
        const env = { ...process.env, LC_ALL: "C", GIT_TERMINAL_PROMPT: "0" };
        execFile("git", args, { cwd, env, timeout: 60_000, maxBuffer: 64 * 1024 * 1024, encoding: "utf8" }, (err, stdout, stderr) => {
          resolve(err === null ? { ok: true, stdout } : { ok: false, stderr: stderr || err.message });
        });
      }),
  };
}
