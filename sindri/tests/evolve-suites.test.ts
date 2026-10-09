import { describe, expect, it } from "vitest";

import { cleanEnvArgv, runSuite } from "../src/evolve/suites.js";
import type { ProcessRunner } from "../src/index/io.js";
import { fakeProc } from "./evolve-fixtures.js";
import { makeDeps } from "./helpers.js";

const withEnv = (env: Record<string, string>) => {
  const base = makeDeps();
  return { ...base, env: { ...base.env, ...env } };
};

describe("cleanEnvArgv", () => {
  it("keeps only HOME and the few variables a suite needs", () => {
    const d = withEnv({ PATH: "/bin", LANG: "C", GITHUB_TOKEN: "secret" });
    expect(cleanEnvArgv(d, ["npm", "test"])).toEqual(["env", "-i", `HOME=${d.home}`, "PATH=/bin", "LANG=C", "npm", "test"]);
    const bare = makeDeps();
    expect(cleanEnvArgv(bare, ["true"])).toEqual(["env", "-i", `HOME=${bare.home}`, "true"]);
  });
});

describe("runSuite", () => {
  it("runs the suite command in the module dir under the heavy lock and reports a scrubbed tail", async () => {
    const d = withEnv({ PATH: "/bin" });
    const proc = fakeProc(() => ({ code: 1, stdout: `line\n${"AKIA" + "ABCDEFGHIJKLMNOP"}\nfailed` }));
    const r = await runSuite(d, proc, "/repo", { id: "package:judge", suite: { argv: ["npm", "test"], cwd: "judge" } });
    expect(proc.calls).toEqual([{ argv: ["env", "-i", `HOME=${d.home}`, "PATH=/bin", "npm", "test"], cwd: "/repo/judge" }]);
    expect(r.ok).toBe(false);
    expect(r.exitCode).toBe(1);
    expect(r.tail).toContain("[REDACTED:aws-access-key]");
    const pass: ProcessRunner = { run: async () => ({ code: 0, stdout: "ok", stderr: "" }) };
    expect((await runSuite(d, pass, "/repo", { id: "x", suite: { argv: ["true"], cwd: "." } })).ok).toBe(true);
  });
});
