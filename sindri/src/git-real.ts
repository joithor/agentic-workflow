import { execFile } from "node:child_process";

import type { GitResult, GitRunner } from "./git.js";

// What git exports to a hook about the repo it runs in; wrong for any other repo.
const REPO_VARS = ["GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_PREFIX"];

export function realGitRunner(): GitRunner {
  return {
    run: (args, cwd, o) =>
      new Promise<GitResult>((resolve) => {
        const env: NodeJS.ProcessEnv = { ...process.env, LC_ALL: "C", GIT_TERMINAL_PROMPT: "0" };
        if (o?.foreign === true) for (const v of REPO_VARS) delete env[v];
        Object.assign(env, o?.env);
        execFile("git", args, { cwd, env, timeout: 60_000, maxBuffer: 64 * 1024 * 1024, encoding: "utf8" }, (err, stdout, stderr) => {
          resolve(err === null ? { ok: true, stdout } : { ok: false, stderr: stderr || err.message });
        });
      }),
  };
}
