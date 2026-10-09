import { spawn } from "node:child_process";

import type { Spawner } from "./model.js";

export function realSpawner(): Spawner {
  return (argv, o) =>
    new Promise((resolve) => {
      const child = spawn(argv[0], argv.slice(1), { cwd: o.cwd, env: o.env, stdio: ["pipe", "pipe", "pipe"] });
      let stdout = "";
      let stderr = "";
      let timedOut = false;
      const timer = setTimeout(() => {
        timedOut = true;
        child.kill("SIGKILL");
      }, o.timeoutMs);
      child.stdout.on("data", (d: Buffer) => (stdout += d.toString("utf8")));
      child.stderr.on("data", (d: Buffer) => (stderr += d.toString("utf8")));
      child.on("close", (code) => {
        clearTimeout(timer);
        resolve({ code: code ?? 1, stdout, stderr, timedOut });
      });
      // A missing binary: no crash, code 127 (the shell's "command not found").
      child.on("error", () => {
        clearTimeout(timer);
        resolve({ code: 127, stdout, stderr, timedOut });
      });
      // Writing to a binary that is missing or exited early raises EPIPE on stdin.
      child.stdin.on("error", () => undefined);
      child.stdin.end(o.stdin);
    });
}
