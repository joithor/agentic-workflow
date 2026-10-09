import fs from "node:fs";

import type { Deps } from "./deps.js";
import { SindriError } from "./errors.js";
import type { ProcessRunner } from "./index/io.js";

// Resolve a secret pointer at the moment of use (spec §11.1). The value is
// returned to the caller only; errors never include it.
export async function resolveSecret(pointer: string, deps: Deps, run: ProcessRunner): Promise<string> {
  const [scheme, ...rest] = pointer.split(":");
  const target = rest.join(":");
  const fail = (why: string): never => {
    throw new SindriError("SND-SECRET-001", `secret ${scheme}:… ${why}`);
  };
  if (scheme === "env") {
    const v = deps.env[target];
    return v !== undefined && v !== "" ? v : fail("is not set");
  }
  if (scheme === "file") {
    const st = fs.statSync(target, { throwIfNoEntry: false });
    if (st === undefined) return fail("file does not exist");
    if ((st.mode & 0o077) !== 0) throw new SindriError("SND-SECRET-002", "secret file is readable by others", { fix: `chmod 600 ${target}` });
    const v = fs.readFileSync(target, "utf8").trim();
    return v !== "" ? v : fail("file is empty");
  }
  const viaProcess = async (argv: string[]): Promise<string> => {
    const r = await run.run(argv, { cwd: "/", timeoutMs: 15_000 });
    const v = r.stdout.trim();
    return r.code === 0 && v !== "" ? v : fail(`could not be read (${argv[0]} exited ${r.code})`);
  };
  if (scheme === "keychain") {
    const [service, account] = target.split("/");
    if (service === undefined || account === undefined || account === "") return fail("must be keychain:service/account");
    return viaProcess(["security", "find-generic-password", "-s", service, "-a", account, "-w"]);
  }
  if (scheme === "op") return viaProcess(["op", "read", `op://${target}`]);
  return fail("has an unknown scheme");
}
