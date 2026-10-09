import { execFile, execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

import type { IndexIo, IndexProbes, ProcessRunner } from "./io.js";

const CLEAN_ENV_KEYS = ["PATH", "HOME", "LANG", "TMPDIR"] as const;

function cleanEnv(): NodeJS.ProcessEnv {
  const env: NodeJS.ProcessEnv = {};
  for (const k of CLEAN_ENV_KEYS) if (process.env[k] !== undefined) env[k] = process.env[k];
  return env;
}

export function realProcessRunner(): ProcessRunner {
  return {
    run: (argv, o) =>
      new Promise((resolve) => {
        execFile(
          argv[0],
          argv.slice(1),
          { cwd: o.cwd, env: { ...(o.cleanEnv === true ? cleanEnv() : process.env), ...o.env }, timeout: o.timeoutMs, maxBuffer: 64 * 1024 * 1024, encoding: "utf8" },
          (err, stdout, stderr) => {
            const code = err === null ? 0 : typeof err.code === "number" ? err.code : 1;
            resolve({ code, stdout, stderr });
          },
        );
      }),
  };
}

export function hasBinary(bin: string): boolean {
  if (path.isAbsolute(bin)) {
    try {
      fs.accessSync(bin, fs.constants.X_OK);
      const st = fs.statSync(bin);
      return st.isFile() && st.uid === 0;
    } catch {
      return false;
    }
  }
  try {
    execFileSync("/bin/sh", ["-c", `command -v "$1"`, "sh", bin], { stdio: "ignore" });
    return true;
  } catch {
    return false;
  }
}

export function realIndexProbes(): IndexProbes {
  const runner = realProcessRunner();
  return {
    has: hasBinary,
    run: (argv, o) => runner.run(argv, o),
    getJson: async (url, timeoutMs) => {
      try {
        const res = await fetch(url, { signal: AbortSignal.timeout(timeoutMs), redirect: "error" });
        return res.ok ? ((await res.json()) as unknown) : null;
      } catch {
        return null;
      }
    },
  };
}

export function realIndexIo(): IndexIo {
  return { fetch: globalThis.fetch, probes: realIndexProbes() };
}
