#!/usr/bin/env node
import os from "node:os";

import { runCli } from "./main.js";
import { realSystemProbe } from "./system-real.js";

const result = await runCli(process.argv.slice(2), {
  env: process.env,
  cwd: process.cwd(),
  home: os.homedir(),
  now: () => new Date(),
  system: realSystemProbe(),
});
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exitCode = result.exitCode;
