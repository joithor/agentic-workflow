#!/usr/bin/env node
import os from "node:os";

import { runCli } from "./main.js";

const result = await runCli(process.argv.slice(2), {
  env: process.env,
  cwd: process.cwd(),
  home: os.homedir(),
  now: () => new Date(),
});
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exitCode = result.exitCode;
