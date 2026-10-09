import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { init } from "../src/evolve/cmd/registry.js";
import { status } from "../src/evolve/cmd/status.js";
import { evolveUsage, makeEvolveCommand, SUBCOMMANDS } from "../src/evolve/commands.js";
import { ringZeroRepo, positiveInt, repoConfig, withLockedWrite, withLockedWriteRetry } from "../src/evolve/ctx.js";
import { SindriError } from "../src/errors.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { stateDir } from "../src/deps.js";
import { COMMANDS } from "../src/main.js";
import { makeDeps } from "./helpers.js";
import { evolveFixture, scriptedEvolveIo, withDeps } from "./evolve-fixtures.js";

const FILES = {
  "config/hooks/done-gate.sh": "#!/bin/sh\n",
  "config/lib/tests/done-gate.test.sh": "#!/bin/sh\n",
  "judge/package.json": "{}",
  "skills/review/SKILL.md": "x\n",
};

describe("sindri evolve init and status", () => {
  it("registers the toolkit, then shows state words with the next command", async () => {
    const fx = await evolveFixture({ files: FILES });
    const empty = await evolveFixture();
    expect((await status([], empty.ctx)).stdout).toBe("No artifacts registered yet.\nNext: sindri evolve init\n");
    expect((await init([], empty.ctx)).stdout).toBe("Registry: 0 artifacts; 0 added, 0 changed, 0 removed; 0 protected, 0 without a suite.\nNext: sindri evolve status\n");

    expect((await init([], fx.ctx)).stdout).toBe(
      "Registry: 3 artifacts (1 hook, 1 package, 1 skill); 3 added, 0 changed, 0 removed; 1 protected, 1 without a suite.\nNext: sindri evolve check --changed\n",
    );
    expect((await init([], fx.ctx)).stdout).toContain("0 added, 0 changed, 0 removed");
    const out = await status([], fx.ctx);
    expect(out.exitCode).toBe(0);
    const row = (state: string, rest: string) => `${state.padEnd(8)} ${rest}`;
    expect(out.stdout).toBe(`${row("untested", "hook:done-gate  protected")}\n${row("untested", "package:judge")}\n${row("no-suite", "skill:review")}\nNext: sindri evolve check --changed\n`);
    const json = JSON.parse((await status(["--json"], fx.ctx)).stdout) as { artifacts: { id: string; state: string }[] };
    expect(json.artifacts.map((a) => [a.id, a.state])).toEqual([["hook:done-gate", "untested"], ["package:judge", "untested"], ["skill:review", "no-suite"]]);
    // The same commands, through the dispatcher.
    expect((await makeEvolveCommand(fx.io)(["init"], fx.deps)).exitCode).toBe(0);
    fx.close();
    empty.close();
  });

  it("shows ok, FAIL and stale (a changed file with no suite run since) and exits 1 for FAIL and stale", async () => {
    const fx = await evolveFixture({ files: FILES });
    await init([], fx.ctx);
    const hashes = Object.fromEntries((fx.ctx.db.prepare("SELECT id, hash FROM artifacts").all() as { id: string; hash: string }[]).map((r) => [r.id, r.hash]));
    const record = (id: string, hash: string, ok: number, seq: string) =>
      fx.ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES (?, ?, ?, 0, ?, ?, 1, ?, 1)").run(id, hash, null, ok, ok === 1 ? 0 : 1, seq);
    record("hook:done-gate", hashes["hook:done-gate"], 1, "t1");
    record("package:judge", hashes["package:judge"], 0, "t2");
    // A run at a channel build is stored with an "at:" hash and never makes an artifact look stale.
    record("hook:done-gate", "at:" + "a".repeat(40), 1, "t3");
    const mixed = await status([], fx.ctx);
    expect(mixed.exitCode).toBe(1);
    const row = (state: string, rest: string) => `${state.padEnd(8)} ${rest}`;
    expect(mixed.stdout).toBe(`${row("ok", "hook:done-gate  protected")}\n${row("FAIL", "package:judge")}\n${row("no-suite", "skill:review")}\nNext: sindri evolve check package:judge   (after fixing)\n`);
    fs.writeFileSync(path.join(fx.repo, "config/hooks/done-gate.sh"), "#!/bin/sh\necho changed\n");
    await init([], fx.ctx);
    const stale = await status([], fx.ctx);
    expect(stale.exitCode).toBe(1);
    expect(stale.stdout).toContain(`${"stale".padEnd(8)} hook:done-gate  protected`);
    fx.close();
  });

  it("falls back to the proposals command when every artifact lacks a suite and nothing is open", async () => {
    const fx = await evolveFixture({ files: { "skills/review/SKILL.md": "x\n" } });
    await init([], fx.ctx);
    const out = await status([], fx.ctx);
    expect(out.exitCode).toBe(0);
    expect(out.stdout).toBe(`${"no-suite".padEnd(8)} skill:review\nNext: sindri evolve proposals\n`);
    fx.close();
  });

  it("counts a skill's open proposals", async () => {
    const fx = await evolveFixture({ files: { "skills/review/SKILL.md": "x\n" } });
    await init([], fx.ctx);
    const insert = fx.ctx.db.prepare(
      "INSERT INTO proposals (id, artifact_id, source, kind, tier, status, title, norm_title, body, created_at, updated_at, epoch) VALUES (?, 'skill:review', 's', 'describe', 'code', ?, 't', 't', 'b', 't', 't', 1)",
    );
    insert.run("p1", "proposed");
    insert.run("p2", "rejected");
    expect((await status([], fx.ctx)).stdout).toContain(`${"no-suite".padEnd(8)} skill:review  1 open proposal(s)\n`);
    fx.close();
  });

  it("registers the seven prompts as artifacts when the dispatcher builds the context", async () => {
    const fx = await evolveFixture();
    const r = await makeEvolveCommand(fx.io)(["init"], fx.deps);
    expect(r.stdout).toContain("Registry: 7 artifacts (7 prompt);");
    expect(r.stdout).toContain("0 protected, 7 without a suite.");
    fx.close();
  });

  it("lists every subcommand in the usage text and the unknown-subcommand error", async () => {
    const fx = await evolveFixture();
    const names = Object.keys(SUBCOMMANDS);
    for (const n of names) expect(evolveUsage()).toContain(n);
    expect(COMMANDS.evolve.usage).toBe(evolveUsage());
    const bad = await makeEvolveCommand(fx.io)(["nope"], fx.deps);
    expect(bad.exitCode).toBe(2);
    expect(bad.stderr).toContain("SND-CLI-002 unknown evolve subcommand: nope; use ");
    for (const n of names) expect(bad.stderr).toContain(n);
    expect((await makeEvolveCommand(fx.io)([], fx.deps)).stderr).toContain("(none)");
    fx.close();
  });

  it("needs an approved profile", async () => {
    const r = await makeEvolveCommand(scriptedEvolveIo(() => null))(["init"], makeDeps());
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-PROFILE-012");
  });
});

describe("evolve context helpers", () => {
  it("finds the ring-0 repo and its config", async () => {
    const fx = await evolveFixture();
    expect(ringZeroRepo(fx.ctx.loaded)).toBe(fx.repo);
    expect(repoConfig(fx.ctx.loaded).defaultBranch).toBe("main");
    fx.close();
  });

  it("takes the tick lock for one write batch and refuses while someone else holds it", async () => {
    const fx = await evolveFixture();
    expect(fx.ctx.write((epoch) => epoch)).toBeGreaterThan(0);
    const held = acquireTickLock({ dir: stateDir(fx.deps), db: fx.ctx.db, sys: fx.deps.system, now: fx.deps.now });
    expect(held.ok).toBe(true);
    let refused: unknown;
    try {
      withLockedWrite(fx.deps, fx.ctx.db, () => 1);
    } catch (e) {
      refused = e;
    }
    expect((refused as SindriError).code).toBe("SND-LOCK-001");
    if (held.ok) held.release();
    expect(withDeps(fx.ctx, {}).write(() => 7)).toBe(7);
    fx.close();
  });

  it("retries a held lock three times, two seconds apart, through deps.sleep, then fails with SND-LOCK-001", async () => {
    const fx = await evolveFixture();
    const held = acquireTickLock({ dir: stateDir(fx.deps), db: fx.ctx.db, sys: fx.deps.system, now: fx.deps.now });
    expect(held.ok).toBe(true);
    const sleeps: number[] = [];
    const never = { ...fx.deps, sleep: async (ms: number) => { sleeps.push(ms); } };
    await expect(withLockedWriteRetry(never, fx.ctx.db, () => 1)).rejects.toMatchObject({ code: "SND-LOCK-001" });
    expect(sleeps).toEqual([2000, 2000, 2000]);
    // The lock frees up during the second wait, so the third attempt succeeds.
    const freed: number[] = [];
    const freeing = { ...fx.deps, sleep: async (ms: number) => { freed.push(ms); if (freed.length === 2 && held.ok) held.release(); } };
    expect(await withLockedWriteRetry(freeing, fx.ctx.db, (epoch) => epoch)).toBeGreaterThan(0);
    expect(freed).toEqual([2000, 2000]);
    // Nothing else is retried: not an ordinary error, not another SindriError.
    await expect(withLockedWriteRetry(never, fx.ctx.db, () => { throw new Error("boom"); })).rejects.toThrow("boom");
    await expect(withLockedWriteRetry(never, fx.ctx.db, () => { throw new SindriError("SND-EVOLVE-001", "not a repo"); })).rejects.toMatchObject({ code: "SND-EVOLVE-001" });
    expect(sleeps).toHaveLength(3);
    expect(await withDeps(fx.ctx, {}).writeRetry(() => 5)).toBe(5);
    fx.close();
  });

  it("parses positive whole-number flags", () => {
    expect(positiveInt(undefined, 20, "--per-hook")).toBe(20);
    expect(positiveInt("5", 20, "--per-hook")).toBe(5);
    for (const bad of ["0", "-1", "1.5", "abc", "12345678"]) expect(() => positiveInt(bad, 20, "--per-hook")).toThrow(/--per-hook must be a positive whole number/);
  });
});
