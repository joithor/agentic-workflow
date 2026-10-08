#!/usr/bin/env node
import os from "node:os";
import readline from "node:readline/promises";

import { realGitRunner } from "./git-real.js";
import { runCli } from "./main.js";
import { realSystemProbe } from "./system-real.js";

const result = await runCli(process.argv.slice(2), {
  env: process.env,
  cwd: process.cwd(),
  home: os.homedir(),
  now: () => new Date(),
  system: realSystemProbe(),
  git: realGitRunner(),
  isTTY: process.stdin.isTTY === true && process.stdout.isTTY === true,
  prompt: async (question) => {
    const rl = readline.createInterface({ input: process.stdin, output: process.stderr });
    try {
      return await rl.question(question);
    } finally {
      rl.close();
    }
  },
});
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exitCode = result.exitCode;
