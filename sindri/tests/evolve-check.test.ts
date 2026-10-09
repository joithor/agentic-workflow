import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { check } from "../src/evolve/cmd/check.js";
import { init } from "../src/evolve/cmd/registry.js";
import type { GitRunner } from "../src/git.js";
import { evolveFixture, fakeProc, scriptedEvolveIo, withDeps } from "./evolve-fixtures.js";

const FILES = {
  "config/hooks/done-gate.sh": "#!/bin/sh\n",
  "config/lib/tests/done-gate.test.sh": "#!/bin/sh\n",
  "judge/package.json": "{}",
  ".agents/rules/a.md": "# a\n",
  ".agents/rules/b.md": "# b\n",
  "scripts/sync-rules.sh": "#!/bin/sh\n",
  "skills/review/SKILL.md": "x\n",
};

// bash suites fail (done-gate), everything else passes.
const handler = (argv: string[]) => (argv.includes("bash") ? { code: 1, stdout: "line1\nboom" } : { code: 0, stdout: "fine" });

async function ready() {
  const proc = fakeProc(handler);
  const fx = await evolveFixture({ files: FILES, io: scriptedEvolveIo(() => null, proc) });
  await init([], fx.ctx);
  return { fx, proc };
}

const rows = (fx: Awaited<ReturnType<typeof evolveFixture>>) =>
  fx.ctx.db.prepare("SELECT artifact_id, ok, dirty, head FROM suite_runs ORDER BY seq").all() as { artifact_id: string; ok: number; dirty: number; head: string | null }[];

describe("sindri evolve check", () => {
  it("runs each distinct suite command once, records a row per artifact, and exits 1 on a failure", async () => {
    const { fx, proc } = await ready();
    const r = await check(["--changed"], fx.ctx);
    expect(r.exitCode).toBe(1);
    expect(proc.calls).toHaveLength(3);
    expect(proc.calls[2].argv.slice(-2)).toEqual(["scripts/sync-rules.sh", "--check"]);
    expect(r.stdout).toMatch(/^FAIL hook:done-gate \(exit 1\)\n {4}line1\n {4}boom\nok {3}package:judge \(\d+\.\d s\)\nok {3}rule:a, rule:b \(\d+\.\d s\)\n/);
    expect(r.stdout).toContain("Checked 3 suite(s): 2 ok, 1 FAILED.");
    expect(r.stdout).toContain("Next: fix the failing suite, then: sindri evolve check hook:done-gate");
    expect(rows(fx).map((x) => [x.artifact_id, x.ok])).toEqual([["hook:done-gate", 0], ["package:judge", 1], ["rule:a", 1], ["rule:b", 1]]);
    expect(rows(fx)[0].head).toMatch(/^[0-9a-f]{40}$/);
    // Only the failed suite runs again.
    await check(["--changed"], fx.ctx);
    expect(proc.calls).toHaveLength(4);
    expect(proc.calls[3].argv).toContain("bash");
    fx.close();
  });

  it("prints what would run with --list, and reports nothing to run once everything passes", async () => {
    const { fx, proc } = await ready();
    const list = await check(["--list", "--changed"], fx.ctx);
    expect(list.exitCode).toBe(0);
    expect(proc.calls).toHaveLength(0);
    expect(list.stdout).toContain("would run bash config/lib/tests/done-gate.test.sh (in .) for hook:done-gate");
    expect(list.stdout).toContain("3 suite(s) would run.");
    expect(list.stdout).toContain("Next: sindri evolve check --changed");
    const only = await check(["package:judge", "rule:a"], fx.ctx);
    expect(only.stdout).toContain("Checked 2 suite(s): 2 ok, 0 FAILED.");
    expect(only.stdout).toContain("Next: sindri evolve status");
    const again = await check(["package:judge", "rule:a", "--changed"], fx.ctx);
    expect(again.stdout).toBe("All suites already pass for the current files. Nothing to run.\nNext: sindri evolve status\n");
    fx.close();
  });

  it("reports an artifact with no suite, an unknown id, and an empty registry", async () => {
    const { fx } = await ready();
    expect((await check(["skill:review"], fx.ctx)).stdout).toBe("skill:review: no suite\nNext: sindri evolve status\n");
    expect((await check(["skill:review", "--changed"], fx.ctx)).stdout).toBe("skill:review: no suite\nNext: sindri evolve status\n");
    await expect(check(["nope"], fx.ctx)).rejects.toThrow(/no such artifact: nope/);
    const bare = await evolveFixture();
    await expect(check([], bare.ctx)).rejects.toThrow(/registry is empty/);
    const json = JSON.parse((await check(["package:judge", "--json"], fx.ctx)).stdout) as { groups: { artifacts: string[]; ok: boolean }[] };
    expect(json.groups).toEqual([{ artifacts: ["package:judge"], ok: true, exitCode: 0, ms: expect.any(Number) }]);
    fx.close();
    bare.close();
  });

  it("records dirty working trees, and treats an unreadable git as no head and not dirty", async () => {
    const { fx } = await ready();
    fs.writeFileSync(path.join(fx.repo, "untracked.txt"), "x");
    const dirty = await check(["package:judge"], fx.ctx);
    expect(dirty.stdout).toContain("Note: the working tree has uncommitted changes; results are bound to the file hashes, not to HEAD.");
    expect(rows(fx).at(-1)).toMatchObject({ artifact_id: "package:judge", dirty: 1 });
    const brokenGit: GitRunner = { run: async () => ({ ok: false, stderr: "fatal" }) };
    await check(["package:judge"], withDeps(fx.ctx, { git: brokenGit }));
    expect(rows(fx).at(-1)).toMatchObject({ dirty: 0, head: null });
    fx.close();
  });

  it("logs a progress line before each suite", async () => {
    const { fx } = await ready();
    const lines: string[] = [];
    await check(["package:judge"], withDeps(fx.ctx, { log: (l) => lines.push(l) }));
    expect(lines).toEqual(["running package:judge (1 of 1)"]);
    fx.close();
  });

  it("says so when no artifact has a suite", async () => {
    const fx = await evolveFixture({ files: { "skills/review/SKILL.md": "x\n" } });
    await init([], fx.ctx);
    expect((await check([], fx.ctx)).stdout).toBe("No artifact has a suite to run.\nNext: sindri evolve status\n");
    fx.close();
  });

  it("runs the mod suite as validate then test, and the adapters suite with the paths as arguments", async () => {
    const proc = fakeProc(() => ({ code: 0 }));
    const fx = await evolveFixture({
      files: { "mods/m/plugin.json": "{}", "config/hooks/adapters/x.sh": "x\n", "config/hooks/tests/codex-adapter.test.sh": "x\n" },
      io: scriptedEvolveIo(() => null, proc),
    });
    await init([], fx.ctx);
    await check(["mod:m", "hook:adapters"], fx.ctx);
    const tails = proc.calls.map((c) => c.argv.slice(c.argv.indexOf("bash")));
    expect(tails[1].slice(0, 3)).toEqual(["bash", "-c", expect.stringContaining('claude plugin validate "$1" && claude plugin test "$1"')]);
    expect(tails[1].slice(-1)).toEqual(["mods/m"]);
    expect(tails[0].slice(-1)).toEqual(["config/hooks/tests/codex-adapter.test.sh"]);
    expect(tails[0][2]).not.toContain("codex-adapter");
    fx.close();
  });
});
