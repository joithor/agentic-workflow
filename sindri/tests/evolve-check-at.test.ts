import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { channelsRoot, writeChannels } from "../src/evolve/channel.js";
import { check } from "../src/evolve/cmd/check.js";
import { init } from "../src/evolve/cmd/registry.js";
import { status } from "../src/evolve/cmd/status.js";
import { evolveFixture, fakeProc, scriptedEvolveIo } from "./evolve-fixtures.js";

const SHA = "d".repeat(40);

async function ready(handler: Parameters<typeof fakeProc>[0] = () => ({ stdout: "fine" }), files: Record<string, string> = { "sindri/package.json": "{}" }) {
  const proc = fakeProc(handler);
  const fx = await evolveFixture({ files, io: scriptedEvolveIo(() => null, proc) });
  await init([], fx.ctx);
  const dir = path.join(channelsRoot(fx.deps), "next", SHA);
  fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
  fs.writeFileSync(path.join(dir, "dist", "cli.js"), "x");
  writeChannels(fx.deps, { stable: null, next: { sha: SHA, dir, installedAt: "2026-10-01T00:00:00Z" } });
  return { fx, proc, dir };
}

describe("sindri evolve check --at", () => {
  it("runs the package suite inside the channel build and records a row bound to that sha", async () => {
    const { fx, proc, dir } = await ready();
    const r = await check(["package:sindri", "--at", SHA], fx.ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(new RegExp(`^ok   package:sindri at dddddddd \\(\\d+\\.\\d s\\)\\nNext: sindri channel promote ${SHA}\\n$`));
    expect(proc.calls).toEqual([{ argv: ["env", "-i", `HOME=${fx.deps.home}`, "npm", "test"], cwd: dir }]);
    expect(fx.ctx.db.prepare("SELECT artifact_id, hash, head, dirty, ok FROM suite_runs").all()).toEqual([{ artifact_id: "package:sindri", hash: `at:${SHA}`, head: SHA, dirty: 0, ok: 1 }]);
    // A run at a channel build never makes the working-tree artifact look stale.
    expect((await status([], fx.ctx)).stdout).toContain(`${"untested".padEnd(8)} package:sindri`);
    expect((await check(["--at", SHA], fx.ctx)).exitCode).toBe(0);
    fx.close();
  });

  it("reports a failure with its tail and the command to rerun", async () => {
    const { fx } = await ready(() => ({ code: 1, stdout: "line1\nboom" }));
    const r = await check(["package:sindri", "--at", SHA], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe(`FAIL package:sindri at dddddddd (exit 1)\n    line1\n    boom\nNext: fix the failing suite, then: sindri evolve check package:sindri --at ${SHA}\n`);
    fx.close();
  });

  it("refuses a bad sha, another artifact, a sha with no build, a broken build, and a registry without the package", async () => {
    const { fx, dir } = await ready();
    await expect(check(["--at", "abc"], fx.ctx)).rejects.toThrow(/--at must be a full 40-character commit sha/);
    await expect(check(["skill:review", "--at", SHA], fx.ctx)).rejects.toThrow(/--at runs package:sindri only/);
    await expect(check(["package:sindri", "judge", "--at", SHA], fx.ctx)).rejects.toThrow(/--at runs package:sindri only/);
    await expect(check(["--at", "e".repeat(40)], fx.ctx)).rejects.toThrow(/no channel build for e{40}/);
    fs.rmSync(path.join(dir, "dist"), { recursive: true });
    await expect(check(["--at", SHA], fx.ctx)).rejects.toThrow(/has no dist\/cli.js/);
    fx.close();
    const bare = await ready(undefined, { "skills/review/SKILL.md": "x\n" });
    await expect(check(["--at", SHA], bare.fx.ctx)).rejects.toThrow(/package:sindri has no suite/);
    bare.fx.close();
  });
});
