import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir } from "../src/deps.js";
import { channelsRoot, writeChannels } from "../src/evolve/channel.js";
import { check } from "../src/evolve/cmd/check.js";
import { init } from "../src/evolve/cmd/registry.js";
import { status } from "../src/evolve/cmd/status.js";
import type { GitResult } from "../src/git.js";
import { evolveFixture, fakeProc, git, scriptedEvolveIo, withDeps } from "./evolve-fixtures.js";

const SHA = "d".repeat(40);

// A fake git that records every call into `events` (shared with the suite runner) and makes the
// worktree dir the way `git worktree add` would.
function fakeGit(events: string[], over: (args: string[]) => GitResult | null = () => null) {
  const calls: { args: string[]; cwd: string; foreign?: boolean }[] = [];
  return {
    calls,
    run: async (args: string[], cwd: string, o?: { foreign?: boolean }): Promise<GitResult> => {
      calls.push({ args, cwd, foreign: o?.foreign });
      events.push(`git ${args.slice(0, 2).join(" ")}`);
      const r = over(args);
      if (r !== null) return r;
      if (args[0] === "worktree" && args[1] === "add") fs.mkdirSync(path.join(args[3] as string, "sindri"), { recursive: true });
      return { ok: true, stdout: "" };
    },
  };
}

async function ready(handler: Parameters<typeof fakeProc>[0] = () => ({ stdout: "fine" }), files: Record<string, string> = { "sindri/package.json": "{}" }) {
  const proc = fakeProc(handler);
  const fx = await evolveFixture({ files, io: scriptedEvolveIo(() => null, proc) });
  await init([], fx.ctx);
  const dir = path.join(channelsRoot(fx.deps), "next", SHA);
  fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
  fs.writeFileSync(path.join(dir, "dist", "cli.js"), "x");
  fs.mkdirSync(path.join(dir, "node_modules"));
  writeChannels(fx.deps, { stable: null, next: { sha: SHA, dir, installedAt: "2026-10-01T00:00:00Z" } });
  return { fx, proc, dir };
}

describe("sindri evolve check --at", () => {
  it("runs the package suite in a detached worktree of the repo at the sha, with the build's node_modules linked", async () => {
    const events: string[] = [];
    let seen: { cwd: string; link: string; linkTarget: string } | null = null;
    const { fx, proc, dir } = await ready((argv, cwd) => {
      events.push("suite");
      seen = { cwd, link: path.join(cwd, "node_modules"), linkTarget: fs.readlinkSync(path.join(cwd, "node_modules")) };
      return { stdout: "fine" };
    });
    const g = fakeGit(events);
    const ctx = withDeps(fx.ctx, { git: g });
    const r = await check(["package:sindri", "--at", SHA], ctx);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(new RegExp(`^ok   package:sindri at dddddddd \\(\\d+\\.\\d s\\)\\nNext: sindri channel promote ${SHA}\\n$`));
    const add = g.calls[0].args;
    const wt = add[3] as string;
    expect(add).toEqual(["worktree", "add", "--detach", wt, SHA]);
    expect(path.dirname(path.dirname(wt))).toBe(path.join(stateDir(fx.deps), "tmp"));
    expect(g.calls.map((c) => c.args.slice(0, 2))).toEqual([["worktree", "add"], ["worktree", "remove"], ["worktree", "prune"]]);
    expect(g.calls[1].args).toEqual(["worktree", "remove", "--force", wt]);
    expect(g.calls.every((c) => c.cwd === fx.repo && c.foreign === true)).toBe(true);
    expect(events).toEqual(["git worktree add", "suite", "git worktree remove", "git worktree prune"]);
    expect(proc.calls).toEqual([{ argv: ["env", "-i", `HOME=${fx.deps.home}`, "npm", "test"], cwd: path.join(wt, "sindri") }]);
    expect(seen).toEqual({ cwd: path.join(wt, "sindri"), link: path.join(wt, "sindri", "node_modules"), linkTarget: path.join(dir, "node_modules") });
    expect(fs.existsSync(path.dirname(wt))).toBe(false);
    expect(fx.ctx.db.prepare("SELECT artifact_id, hash, head, dirty, ok FROM suite_runs").all()).toEqual([{ artifact_id: "package:sindri", hash: `at:${SHA}`, head: SHA, dirty: 0, ok: 1 }]);
    // A run at a channel build never makes the working-tree artifact look stale.
    expect((await status([], fx.ctx)).stdout).toContain(`${"untested".padEnd(8)} package:sindri`);
    fx.close();
  });

  it("really checks out the sha in a real worktree, runs there, and leaves the repo's worktrees as they were", async () => {
    const seen: { cwd: string; head: string; hasPkg: boolean; modules: boolean }[] = [];
    const proc = fakeProc((argv, cwd) => {
      seen.push({ cwd, head: git(cwd, "rev-parse", "HEAD").trim(), hasPkg: fs.existsSync(path.join(cwd, "package.json")), modules: fs.existsSync(path.join(cwd, "node_modules", "marker")) });
      return {};
    });
    const fx = await evolveFixture({ files: { "sindri/package.json": "{}" }, io: scriptedEvolveIo(() => null, proc) });
    await init([], fx.ctx);
    const head = git(fx.repo, "rev-parse", "HEAD").trim();
    const dir = path.join(channelsRoot(fx.deps), "next", head);
    fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
    fs.writeFileSync(path.join(dir, "dist", "cli.js"), "x");
    fs.mkdirSync(path.join(dir, "node_modules"));
    fs.writeFileSync(path.join(dir, "node_modules", "marker"), "m");
    writeChannels(fx.deps, { stable: null, next: { sha: head, dir, installedAt: "2026-10-01T00:00:00Z" } });
    const before = git(fx.repo, "status", "--porcelain");
    expect((await check(["--at", head], fx.ctx)).exitCode).toBe(0);
    expect(seen).toHaveLength(1);
    expect(seen[0]).toMatchObject({ head, hasPkg: true, modules: true });
    expect(seen[0].cwd.endsWith(`${path.sep}wt${path.sep}sindri`)).toBe(true);
    expect(fs.existsSync(seen[0].cwd)).toBe(false);
    expect(git(fx.repo, "worktree", "list", "--porcelain").match(/^worktree /gm)).toHaveLength(1);
    expect(git(fx.repo, "status", "--porcelain")).toBe(before);
    fx.close();
  });

  it("removes the worktree when the suite fails, and when it throws; a cleanup failure is logged, not thrown", async () => {
    const events: string[] = [];
    const failing = await ready(() => { events.push("suite"); return { code: 1, stdout: "boom" }; });
    const g1 = fakeGit(events);
    expect((await check(["--at", SHA], withDeps(failing.fx.ctx, { git: g1 }))).exitCode).toBe(1);
    expect(events).toEqual(["git worktree add", "suite", "git worktree remove", "git worktree prune"]);
    failing.fx.close();

    const logs: string[] = [];
    const throwing = await ready(() => { throw new Error("runner died"); });
    const g2 = fakeGit([], (args) => (args[1] === "remove" ? { ok: false, stderr: "cannot remove" } : args[1] === "prune" ? ({ ok: false, stderr: "" } as GitResult) : null));
    await expect(check(["--at", SHA], withDeps(throwing.fx.ctx, { git: g2, log: (m) => logs.push(m) }))).rejects.toThrow(/runner died/);
    expect(g2.calls.map((c) => c.args[1])).toEqual(["add", "remove", "prune"]);
    expect(logs.join("\n")).toMatch(/could not remove the temporary worktree.*cannot remove/);
    expect(logs.join("\n")).toMatch(/could not prune/);
    throwing.fx.close();

    // A git that throws during cleanup is logged too, and the suite result still wins.
    const ok = await ready();
    const logs2: string[] = [];
    const g3 = fakeGit([], (args) => { if (args[1] === "remove") throw new Error("git gone"); return null; });
    expect((await check(["--at", SHA], withDeps(ok.fx.ctx, { git: g3, log: (m) => logs2.push(m) }))).exitCode).toBe(0);
    expect(logs2.join("\n")).toMatch(/git gone/);
    ok.fx.close();
  });

  it("refuses a build without node_modules before creating any worktree", async () => {
    const { fx, proc, dir } = await ready();
    fs.rmSync(path.join(dir, "node_modules"), { recursive: true });
    const g = fakeGit([]);
    await expect(check(["--at", SHA], withDeps(fx.ctx, { git: g }))).rejects.toThrow(/has no node_modules/);
    expect(g.calls).toEqual([]);
    expect(proc.calls).toEqual([]);
    fx.close();
  });

  it("gives a clear error and runs no suite when the worktree can't be created (unknown sha)", async () => {
    const { fx, proc } = await ready();
    const g = fakeGit([], (args) => (args[1] === "add" ? { ok: false, stderr: "fatal: invalid reference" } : null));
    await expect(check(["--at", SHA], withDeps(fx.ctx, { git: g }))).rejects.toThrow(/could not check out dddddddd.*invalid reference/);
    expect(proc.calls).toEqual([]);
    expect(g.calls.map((c) => c.args[1])).toEqual(["add", "remove", "prune"]);
    expect(fx.ctx.db.prepare("SELECT COUNT(*) AS n FROM suite_runs").get()).toEqual({ n: 0 });
    fx.close();
  });

  it("reports a failure with its tail and the command to rerun", async () => {
    const { fx } = await ready(() => ({ code: 1, stdout: "line1\nboom" }));
    const r = await check(["package:sindri", "--at", SHA], withDeps(fx.ctx, { git: fakeGit([]) }));
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
