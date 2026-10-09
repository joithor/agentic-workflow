import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import type { ProcessRunner } from "../src/index/io.js";
import { resolveSecret } from "../src/secrets.js";
import { makeDeps, tempDir } from "./helpers.js";

const runner = (code: number, stdout: string): ProcessRunner & { argv: string[][] } => {
  const argv: string[][] = [];
  return { argv, run: async (a) => { argv.push(a); return { code, stdout, stderr: "" }; } };
};

describe("resolveSecret", () => {
  it("reads env, private files, the macOS keychain and 1Password", async () => {
    const d = makeDeps({ env: { LIN: "tok-env" } });
    expect(await resolveSecret("env:LIN", d, runner(0, ""))).toBe("tok-env");
    const file = path.join(tempDir(), "t");
    fs.writeFileSync(file, "tok-file\n", { mode: 0o600 });
    expect(await resolveSecret(`file:${file}`, d, runner(0, ""))).toBe("tok-file");
    const kc = runner(0, "tok-kc\n");
    expect(await resolveSecret("keychain:linear/me", d, kc)).toBe("tok-kc");
    expect(kc.argv[0]).toEqual(["security", "find-generic-password", "-s", "linear", "-a", "me", "-w"]);
    const op = runner(0, "tok-op\n");
    expect(await resolveSecret("op:Work/Linear/credential", d, op)).toBe("tok-op");
    expect(op.argv[0]).toEqual(["op", "read", "op://Work/Linear/credential"]);
  });

  it("refuses unreadable, empty, world-readable or malformed pointers without echoing values", async () => {
    const d = makeDeps({ env: {} });
    await expect(resolveSecret("env:NOPE", d, runner(0, ""))).rejects.toMatchObject({ code: "SND-SECRET-001" });
    const loose = path.join(tempDir(), "t");
    fs.writeFileSync(loose, "tok", { mode: 0o644 });
    fs.chmodSync(loose, 0o644);
    await expect(resolveSecret(`file:${loose}`, d, runner(0, ""))).rejects.toMatchObject({ code: "SND-SECRET-002" });
    const empty = path.join(tempDir(), "e");
    fs.writeFileSync(empty, "  \n", { mode: 0o600 });
    await expect(resolveSecret(`file:${empty}`, d, runner(0, ""))).rejects.toThrow(/file is empty/);
    await expect(resolveSecret("file:/no/such/file", d, runner(0, ""))).rejects.toMatchObject({ code: "SND-SECRET-001" });
    await expect(resolveSecret("keychain:linear/me", d, runner(44, ""))).rejects.toMatchObject({ code: "SND-SECRET-001" });
    await expect(resolveSecret("keychain:noslash", d, runner(0, "x"))).rejects.toMatchObject({ code: "SND-SECRET-001" });
    await expect(resolveSecret("keychain:linear/", d, runner(0, "x"))).rejects.toThrow(/keychain:service\/account/);
    await expect(resolveSecret("op:Work/Linear", d, runner(1, ""))).rejects.toMatchObject({ code: "SND-SECRET-001" });
    await expect(resolveSecret("vault:x", d, runner(0, "x"))).rejects.toMatchObject({ code: "SND-SECRET-001" });
  });
});
