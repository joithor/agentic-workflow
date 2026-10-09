import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir } from "../src/deps.js";
import { LEDGER_SCHEMA_VERSION } from "../src/ledger/db.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { channelsRoot, readChannels, wrapperTarget, writeChannels, writeWrapper } from "../src/evolve/channel.js";
import { makeChannelCommand } from "../src/evolve/cmd/channel.js";
import type { GitRunner } from "../src/git.js";
import { evolveFixture, fakeProc } from "./evolve-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

const A = "a".repeat(40);
const B = "b".repeat(40);
const entry = (sha: string, dir: string, at = "2026-10-01T00:00:00Z", schema: number | null = LEDGER_SCHEMA_VERSION) => ({ sha, dir, installedAt: at, ...(schema === null ? {} : { schema }) });

async function ready(o: { tty?: string | null; git?: GitRunner; bin?: string } = {}) {
  const fx = await evolveFixture();
  const asked: string[] = [];
  const deps = {
    ...fx.deps,
    env: { ...fx.deps.env, CLAUDE_LOCAL_BIN: o.bin ?? tempDir() },
    git: o.git ?? fx.deps.git,
    isTTY: o.tty !== undefined && o.tty !== null,
    prompt: async (q: string) => {
      asked.push(q);
      return o.tty ?? "";
    },
  };
  const mk = (channel: "stable" | "next", sha: string): string => {
    const dir = path.join(channelsRoot(deps), channel, sha);
    fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
    fs.writeFileSync(path.join(dir, "dist", "cli.js"), "x");
    return dir;
  };
  const proc = fakeProc(() => ({}));
  const run = (args: string[]) => makeChannelCommand({ process: proc })(args, deps);
  const record = (sha: string, ok = 1) =>
    fx.ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES ('package:sindri', ?, ?, 0, ?, 0, 1, 't', 1)").run(`at:${sha}`, sha, ok);
  return { fx, deps, asked, mk, run, record, proc };
}

describe("sindri channel status", () => {
  it("says so when nothing is recorded, then shows both channels, the reason next can't be promoted, and the wrapper", async () => {
    const t = await ready();
    expect((await t.run(["status"])).stdout).toBe("No channels recorded.\nNext: scripts/install-sindri.sh --channel next --ref <sha>\n");
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    const r = await t.run(["status"]);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(
      [
        "stable aaaaaaaa (since 2026-10-01); previous none",
        `next   bbbbbbbb (since 2026-10-01); no passing package:sindri suite run for ${B}; run: sindri evolve check package:sindri --at ${B}`,
        "wrapper: none found",
        `Next: sindri evolve check package:sindri --at ${B}`,
        "",
      ].join("\n"),
    );
    t.record(B);
    expect((await t.run(["status"])).stdout).toContain(`next   bbbbbbbb (since 2026-10-01); soaked 7 days on next; suite passed at that sha\n`);
    expect(JSON.parse((await t.run(["status", "--json"])).stdout)).toMatchObject({ next: { sha: B }, canPromote: { ok: true } });
    writeWrapper(t.deps, path.join(dirA, "dist", "cli.js"));
    expect((await t.run(["status"])).stdout).toContain(`wrapper: runs ${path.join(dirA, "dist", "cli.js")}\n`);
    writeWrapper(t.deps, "/somewhere/else/dist/cli.js");
    const odd = await t.run(["status"]);
    expect(odd.exitCode).toBe(1);
    expect(odd.stdout).toContain("(does not match stable; an in-place install may have overwritten it)");
    writeChannels(t.deps, { stable: null, next: entry(B, dirB) });
    expect((await t.run(["status"])).stdout).toContain("stable none\n");
    t.fx.close();
  });
});

describe("sindri channel status with no next build", () => {
  it("shows the previous stable build and says nothing is on next", async () => {
    const t = await ready();
    writeChannels(t.deps, { stable: { ...entry(B, t.mk("stable", B)), previous: entry(A, t.mk("stable", A)) }, next: null });
    const r = await t.run(["status"]);
    expect(r.stdout).toBe(
      [
        "stable bbbbbbbb (since 2026-10-01); previous aaaaaaaa",
        "next   none",
        "wrapper: none found",
        "Next: scripts/install-sindri.sh --channel next --ref <sha>",
        "",
      ].join("\n"),
    );
    t.fx.close();
  });
});

describe("sindri channel promote (Review Focus: human-only, protected paths shown)", () => {
  const protectedDiff: GitRunner = { run: async (args) => (args[0] === "diff" ? { ok: true, stdout: "sindri/src/scrub/patterns.ts\nskills/review/SKILL.md\n" } : { ok: false, stderr: "" }) };

  it("refuses until the soak, the suite run, a terminal and the typed confirmation are all there", async () => {
    const t = await ready({ tty: null });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB, "2026-10-07T00:00:00Z") });
    expect((await t.run(["promote", B])).stderr).toContain("SND-EVOLVE-005 next has soaked 1 of 3 days");
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    const noSuite = await t.run(["promote", B]);
    expect(noSuite.stderr).toContain(`SND-EVOLVE-005 no passing package:sindri suite run for ${B}`);
    expect(noSuite.stderr).toContain(`fix: sindri evolve check package:sindri --at ${B}`);
    t.record(B);
    expect((await t.run(["promote", B])).stderr).toContain("SND-EVOLVE-006");
    expect((await t.run(["promote", "bbbb"])).stderr).toContain("SND-CLI-002 usage: sindri channel promote <40-character sha>");
    expect((await t.run(["promote"])).stderr).toContain("SND-CLI-002");
    t.fx.close();
    const wrong = await ready({ tty: "nope" });
    const wa = wrong.mk("stable", A);
    const wb = wrong.mk("next", B);
    writeChannels(wrong.deps, { stable: { ...entry(A, wa), previous: null }, next: entry(B, wb) });
    wrong.record(B);
    expect((await wrong.run(["promote", B])).stderr).toContain("SND-EVOLVE-007");
    expect(readChannels(wrong.deps).stable?.sha).toBe(A);
    wrong.fx.close();
  });

  it("uses the latest suite run at the sha: a later failing rerun withdraws an earlier pass", async () => {
    const t = await ready({ tty: "bbbbbbbb" });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    t.record(B, 1);
    t.record(B, 0);
    expect((await t.run(["promote", B])).stderr).toContain(`SND-EVOLVE-005 no passing package:sindri suite run for ${B}`);
    expect((await t.run(["status"])).stdout).toContain("no passing package:sindri suite run");
    t.record(B, 1);
    expect((await t.run(["promote", B])).exitCode).toBe(0);
    t.fx.close();
  });

  it("refuses a sha that is already stable, so the real rollback target is never overwritten", async () => {
    const t = await ready({ tty: "bbbbbbbb" });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("stable", B);
    writeChannels(t.deps, { stable: { ...entry(B, dirB), previous: entry(A, dirA) }, next: entry(B, t.mk("next", B)) });
    t.record(B);
    const r = await t.run(["promote", B]);
    expect(r.exitCode).not.toBe(0);
    expect(r.stderr).toContain(`SND-EVOLVE-005 ${B} is already stable`);
    expect(t.asked).toEqual([]);
    expect(readChannels(t.deps).stable).toMatchObject({ sha: B, previous: { sha: A } });
    expect(t.fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([]);
    t.fx.close();
  });

  it("shows the protected paths that changed since stable, then promotes and records it", async () => {
    const t = await ready({ tty: "bbbbbbbb", git: protectedDiff });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    t.record(B);
    const r = await t.run(["promote", B]);
    expect(r.stdout).toBe("Promoted bbbbbbbb to stable. Roll back with: sindri channel rollback\nNext: sindri channel status\n");
    expect(t.asked[0]).toBe("Promote bbbbbbbb to stable?\nProtected paths changed since stable: sindri/src/scrub/patterns.ts\nType the first 8 characters of the sha to confirm: ");
    expect(wrapperTarget(t.deps)).toBe(path.join(dirB, "dist", "cli.js"));
    expect(readChannels(t.deps).stable).toMatchObject({ sha: B, previous: { sha: A } });
    expect(t.fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit").all()).toEqual([{ verb: "promote", detail: `${B} (previous ${A})` }]);
    t.fx.close();
  });

  it("words the prompt for a clean diff, an unreadable diff and a first stable build", async () => {
    const clean = await ready({ tty: "bbbbbbbb", git: { run: async () => ({ ok: true, stdout: "skills/review/SKILL.md\n" }) } });
    writeChannels(clean.deps, { stable: { ...entry(A, clean.mk("stable", A)), previous: null }, next: entry(B, clean.mk("next", B)) });
    clean.record(B);
    await clean.run(["promote", B]);
    expect(clean.asked[0]).toContain("No protected paths changed since stable.");
    clean.fx.close();
    const broken = await ready({ tty: "bbbbbbbb", git: { run: async () => ({ ok: false, stderr: "bad object" }) } });
    writeChannels(broken.deps, { stable: { ...entry(A, broken.mk("stable", A)), previous: null }, next: entry(B, broken.mk("next", B)) });
    broken.record(B);
    await broken.run(["promote", B]);
    expect(broken.asked[0]).toContain("Couldn't list the changes since stable.");
    broken.fx.close();
    const first = await ready({ tty: "bbbbbbbb" });
    writeChannels(first.deps, { stable: null, next: entry(B, first.mk("next", B)) });
    first.record(B);
    await first.run(["promote", B]);
    expect(first.asked[0]).toContain("This is the first stable build.");
    first.fx.close();
  });
});

describe("sindri channel rollback", () => {
  it("refuses before asking when the previous build's ledger schema is older than the ledger or unrecorded", async () => {
    const t = await ready({ tty: "aaaaaaaa" });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("stable", B);
    writeChannels(t.deps, { stable: { ...entry(B, dirB), previous: entry(A, dirA, undefined, LEDGER_SCHEMA_VERSION - 1) }, next: null });
    const old = await t.run(["rollback"]);
    expect(old.exitCode).not.toBe(0);
    expect(old.stderr).toContain(`SND-EVOLVE-005 aaaaaaaa understands ledger schema v${LEDGER_SCHEMA_VERSION - 1}, but the ledger is at v${LEDGER_SCHEMA_VERSION}`);
    writeChannels(t.deps, { stable: { ...entry(B, dirB), previous: entry(A, dirA, undefined, null) }, next: null });
    expect((await t.run(["rollback"])).stderr).toContain("aaaaaaaa has no recorded ledger schema");
    expect(t.asked).toEqual([]);
    expect(readChannels(t.deps).stable?.sha).toBe(B);
    t.fx.close();
  });

  it("needs a terminal and a typed confirmation, then points stable back at the previous build", async () => {
    const t = await ready({ tty: null });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("stable", B);
    writeChannels(t.deps, { stable: { ...entry(B, dirB), previous: entry(A, dirA) }, next: null });
    expect((await t.run(["rollback"])).stderr).toContain("SND-EVOLVE-006");
    t.fx.close();
    const wrong = await ready({ tty: "nope" });
    writeChannels(wrong.deps, { stable: { ...entry(B, wrong.mk("stable", B)), previous: entry(A, wrong.mk("stable", A)) }, next: null });
    expect((await wrong.run(["rollback"])).stderr).toContain("SND-EVOLVE-007");
    wrong.fx.close();
    const ok = await ready({ tty: "aaaaaaaa" });
    const a = ok.mk("stable", A);
    writeChannels(ok.deps, { stable: { ...entry(B, ok.mk("stable", B)), previous: entry(A, a) }, next: null });
    const r = await ok.run(["rollback"]);
    expect(r.stdout).toBe("Rolled back to aaaaaaaa. There is no previous build now; to go forward, promote a build from next.\nNext: sindri channel status\n");
    expect(ok.asked[0]).toBe("Roll stable back to aaaaaaaa?\nType the first 8 characters of the sha to confirm: ");
    expect(readChannels(ok.deps).stable?.sha).toBe(A);
    expect(ok.fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([{ verb: "rollback" }]);
    expect(readChannels(ok.deps).stable?.previous).toBeNull();
    expect((await ok.run(["rollback"])).stderr).toContain("SND-EVOLVE-005 there is no previous stable build to roll back to");
    const none = await ready({ tty: "x" });
    expect((await none.run(["rollback"])).stderr).toContain("SND-EVOLVE-005 there is no previous stable build to roll back to");
    ok.fx.close();
    none.fx.close();
  });
});

describe("what unlocks promote (gate rows)", () => {
  const insert = (t: Awaited<ReturnType<typeof ready>>, hash: string, head: string, dirty: number, ok: number) =>
    t.fx.ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES ('package:sindri', ?, ?, ?, ?, 0, 1, 't', 1)").run(hash, head, dirty, ok);

  it("is not unlocked by a run at another sha, a failed run, a dirty run, or a run of the checkout rather than the build", async () => {
    const t = await ready({ tty: "bbbbbbbb" });
    writeChannels(t.deps, { stable: { ...entry(A, t.mk("stable", A)), previous: null }, next: entry(B, t.mk("next", B)) });
    insert(t, `at:${A}`, A, 0, 1);
    insert(t, `at:${B}`, B, 0, 0);
    insert(t, `at:${B}`, B, 1, 1);
    insert(t, "checkout-hash", B, 0, 1);
    const refused = await t.run(["promote", B]);
    expect(refused.stderr).toContain(`SND-EVOLVE-005 no passing package:sindri suite run for ${B}`);
    expect(readChannels(t.deps).stable?.sha).toBe(A);
    t.record(B);
    expect((await t.run(["promote", B])).stdout).toContain("Promoted bbbbbbbb");
    t.fx.close();
  });

  it("refuses, and changes nothing, when the next build won't start", async () => {
    const t = await ready({ tty: "bbbbbbbb" });
    const proc = fakeProc(() => ({ code: 3 }));
    const dirA = t.mk("stable", A);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, t.mk("next", B)) });
    t.record(B);
    const r = await makeChannelCommand({ process: proc })(["promote", B], t.deps);
    expect(r.stderr).toContain("didn't start (--version exited 3)");
    expect(readChannels(t.deps).stable?.sha).toBe(A);
    expect(t.fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([]);
    t.fx.close();
  });

  it("retries a held tick lock when it writes the audit row, instead of failing after the switch", async () => {
    const t = await ready({ tty: "bbbbbbbb" });
    writeChannels(t.deps, { stable: { ...entry(A, t.mk("stable", A)), previous: null }, next: entry(B, t.mk("next", B)) });
    t.record(B);
    const held = acquireTickLock({ dir: stateDir(t.deps), db: t.fx.ctx.db, sys: t.deps.system, now: t.deps.now });
    expect(held.ok).toBe(true);
    const sleeps: number[] = [];
    const deps = { ...t.deps, sleep: async (ms: number) => { sleeps.push(ms); if (held.ok) held.release(); } };
    const r = await makeChannelCommand({ process: t.proc })(["promote", B], deps);
    expect(r.exitCode).toBe(0);
    expect(sleeps).toEqual([2000]);
    expect(t.fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([{ verb: "promote" }]);
    t.fx.close();
  });
});

describe("the channel dispatcher", () => {
  it("rejects an unknown subcommand and needs an approved profile", async () => {
    const t = await ready();
    expect((await t.run(["nope"])).stderr).toContain("SND-CLI-002 unknown channel subcommand: nope; use status, promote, rollback");
    expect((await t.run([])).stderr).toContain("(none)");
    const bare = await makeChannelCommand({ process: t.proc })(["status"], makeDeps());
    expect(bare.stderr).toContain("SND-PROFILE-012");
    t.fx.close();
  });
});
