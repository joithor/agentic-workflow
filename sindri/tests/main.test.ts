import { describe, expect, it } from "vitest";

import { stateDir } from "../src/deps.js";
import { COMMANDS, runCli } from "../src/main.js";
import { success } from "../src/output.js";
import { makeDeps } from "./helpers.js";

describe("runCli", () => {
  it("prints help for no args, help and --help", async () => {
    for (const argv of [[], ["help"], ["--help"]]) {
      const r = await runCli(argv, makeDeps());
      expect(r.exitCode).toBe(0);
      expect(r.stdout).toContain("Usage: sindri <command>");
      expect(r.stdout).toContain("help");
    }
  });

  it("prints the version", async () => {
    const r = await runCli(["--version"], makeDeps());
    expect(r.stdout).toMatch(/^\d+\.\d+\.\d+\n$/);
  });

  it("rejects an unknown command with SND-CLI-001 and exit 2, including inherited names", async () => {
    for (const name of ["frob", "constructor", "toString"]) {
      const r = await runCli([name], makeDeps());
      expect(r.exitCode).toBe(2);
      expect(r.stderr).toContain(`SND-CLI-001 unknown command: ${name}`);
    }
  });

  it("prints a command's usage for <command> --help, and turns a crash into SND-CLI-900", async () => {
    COMMANDS.boom = { summary: "test only", usage: "Usage: sindri boom", run: async () => { throw new TypeError("kaboom"); } };
    COMMANDS.aaa = { summary: "also test only", usage: "Usage: sindri aaa", run: async () => success("ok", null, false) };
    try {
      const listed = (await runCli(["help"], makeDeps())).stdout;
      expect(listed.indexOf("aaa")).toBeLessThan(listed.indexOf("boom"));
      expect(await runCli(["boom", "--help"], makeDeps())).toEqual({ exitCode: 0, stdout: "Usage: sindri boom\n", stderr: "" });
      const crash = await runCli(["boom"], makeDeps());
      expect(crash.exitCode).toBe(2);
      expect(crash.stderr).toBe("SND-CLI-900 unexpected error: kaboom\n  fix: rerun with SINDRI_DEBUG=1 and report the output\n");
      const debug = await runCli(["boom"], makeDeps({ env: { SINDRI_DEBUG: "1" } }));
      expect(debug.stderr).toContain("TypeError: kaboom");
      const json = JSON.parse((await runCli(["boom", "--json"], makeDeps())).stdout);
      expect(json.error.code).toBe("SND-CLI-900");
      COMMANDS.boom.run = async () => { throw "not an Error"; };
      expect((await runCli(["boom"], makeDeps())).stderr).toContain("unexpected error: not an Error");
      COMMANDS.boom.run = async () => { const e = new Error("no stack"); e.stack = undefined; throw e; };
      expect((await runCli(["boom"], makeDeps({ env: { SINDRI_DEBUG: "1" } }))).stderr).toContain("unexpected error: no stack");
    } finally {
      delete COMMANDS.boom;
      delete COMMANDS.aaa;
    }
  });

  it("scrubs secrets out of SND-CLI-900 text and its debug stack (invariant 8)", async () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    COMMANDS.boom = { summary: "test only", usage: "Usage: sindri boom", run: async () => { throw new Error(`bad key ${secret}`); } };
    try {
      for (const env of [{}, { SINDRI_DEBUG: "1" }]) {
        for (const flags of [[], ["--json"]]) {
          const r = await runCli(["boom", ...flags], makeDeps({ env }));
          expect(r.stdout + r.stderr).not.toContain(secret);
          expect(r.stdout + r.stderr).toContain("bad key [REDACTED:aws-access-key]");
        }
      }
    } finally {
      delete COMMANDS.boom;
    }
  });

  it("stateDir honors AW_STATE_DIR and falls back to ~/.agentic-workflow", () => {
    expect(stateDir(makeDeps({ env: { AW_STATE_DIR: "/x" } }))).toBe("/x/sindri");
    expect(stateDir(makeDeps({ env: {}, home: "/h" }))).toBe("/h/.agentic-workflow/sindri");
  });
});
