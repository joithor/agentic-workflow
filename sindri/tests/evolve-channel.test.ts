import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { binDir, buildProblem, canPromote, channelsRoot, promote, readChannels, rollback, wrapperTarget, wrapperText, writeChannels, writeWrapper, type ChannelState } from "../src/evolve/channel.js";
import { stateDir } from "../src/deps.js";
import { LEDGER_SCHEMA_VERSION } from "../src/ledger/db.js";
import { fakeProc } from "./evolve-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

const A = "a".repeat(40);
const B = "b".repeat(40);
const C = "c".repeat(40);
const DAY = 86_400_000;
const SCHEMA = LEDGER_SCHEMA_VERSION;
const entry = (sha: string, dir: string, at = "2026-10-01T00:00:00Z", schema?: number) => ({ sha, dir, installedAt: at, ...(schema === undefined ? {} : { schema }) });

function deps(bin = tempDir()) {
  const d = makeDeps();
  return { ...d, env: { ...d.env, CLAUDE_LOCAL_BIN: bin } };
}
function build(d: ReturnType<typeof deps>, channel: "stable" | "next", sha: string): string {
  const dir = path.join(channelsRoot(d), channel, sha);
  fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
  fs.writeFileSync(path.join(dir, "dist", "cli.js"), 'process.stdout.write("ran " + process.argv.slice(2).join(" "));');
  return dir;
}

describe("channels.json", () => {
  it("reads a missing file as empty, writes atomically and privately, and refuses a corrupt or unreadable one", () => {
    const d = deps();
    const file = path.join(stateDir(d), "channels.json");
    expect(readChannels(d)).toEqual({ stable: null, next: null });
    const state: ChannelState = { stable: { ...entry(A, "/x"), previous: null }, next: entry(B, "/y") };
    writeChannels(d, state);
    expect(readChannels(d)).toEqual(state);
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    expect(fs.readdirSync(stateDir(d)).filter((n) => n.includes(".tmp-"))).toEqual([]);
    fs.writeFileSync(file, "{ not json");
    expect(() => readChannels(d)).toThrow(/channels.json is corrupt/);
    fs.writeFileSync(file, JSON.stringify({ stable: null, next: { sha: "not-a-sha", dir: "x", installedAt: "t" } }));
    expect(() => readChannels(d)).toThrow(/channels.json is corrupt/);
    fs.rmSync(file);
    fs.mkdirSync(file);
    expect(() => readChannels(d)).toThrow(/EISDIR/);
  });
});

describe("buildProblem", () => {
  it("accepts a build under the channels root with a dist/cli.js, and names the problem otherwise", () => {
    const d = deps();
    const dir = build(d, "next", B);
    expect(buildProblem(d, entry(B, dir))).toBeNull();
    expect(buildProblem(d, entry(B, "/nope"))).toBe("the build at /nope doesn't exist");
    const bare = path.join(channelsRoot(d), "next", C);
    fs.mkdirSync(bare, { recursive: true });
    expect(buildProblem(d, entry(C, bare))).toBe(`${bare} has no dist/cli.js`);
    const outside = tempDir();
    fs.mkdirSync(path.join(outside, "dist"));
    fs.writeFileSync(path.join(outside, "dist", "cli.js"), "x");
    expect(buildProblem(d, entry(C, outside))).toBe(`${outside} is outside the channels directory`);
    const link = path.join(channelsRoot(d), "next", "link");
    fs.symlinkSync(outside, link);
    expect(buildProblem(d, entry(C, link))).toBe(`${link} is outside the channels directory`);
  });
});

describe("canPromote (spec §7.7)", () => {
  const now = new Date("2026-10-10T00:00:00Z");
  const state = (nextAgeDays: number | null): ChannelState => ({
    stable: { ...entry(A, "/x"), previous: null },
    next: nextAgeDays === null ? null : entry(B, "/y", new Date(now.getTime() - nextAgeDays * DAY).toISOString()),
  });

  it("needs the sha on next, the soak, and a passing suite run at that sha, and always says what to do next", () => {
    expect(canPromote(state(4), B, true, now)).toEqual({ ok: true, why: "soaked 4 days on next; suite passed at that sha", next: `sindri channel promote ${B}` });
    expect(canPromote(state(4), C, true, now)).toEqual({ ok: false, why: `${C} is not what next runs (${B})`, next: `sindri channel promote ${B}` });
    expect(canPromote(state(4), B, false, now)).toEqual({
      ok: false, why: `no passing package:sindri suite run for ${B}; run: sindri evolve check package:sindri --at ${B}`, next: `sindri evolve check package:sindri --at ${B}`,
    });
    expect(canPromote(state(1), B, true, now)).toEqual({ ok: false, why: "next has soaked 1 of 3 days", next: "sindri channel status (after the soak)" });
    expect(canPromote(state(null), B, true, now)).toEqual({ ok: false, why: "nothing is installed on next", next: "scripts/install-sindri.sh --channel next --ref <sha>" });
  });

  it("fails closed when installedAt is not a date: no soak can be proven, so nothing promotes", () => {
    for (const bad of ["", "yesterday", "2026-13-45"]) {
      const c: ChannelState = { stable: null, next: entry(B, "/y", bad) };
      const r = canPromote(c, B, true, now);
      expect(r.ok).toBe(false);
      expect(r.why).toBe(`next has no readable install time (${JSON.stringify(bad)}), so its soak can't be checked`);
    }
  });
});

describe("the wrapper", () => {
  it("single-quotes every path, and replaces a symlink at bin/sindri instead of writing through it", () => {
    const bin = path.join(tempDir(), "b'in");
    const d = deps(bin);
    const cli = path.join(build(d, "next", B), "dist", "cli.js");
    fs.mkdirSync(bin, { recursive: true });
    const victim = path.join(tempDir(), "victim.txt");
    fs.writeFileSync(victim, "keep me");
    fs.symlinkSync(victim, path.join(bin, "sindri"));
    writeWrapper(d, cli);
    expect(fs.readFileSync(victim, "utf8")).toBe("keep me");
    expect(fs.lstatSync(path.join(bin, "sindri")).isSymbolicLink()).toBe(false);
    expect(fs.readFileSync(path.join(bin, "sindri"), "utf8")).toContain(`export SINDRI_BIN='${path.join(bin, "sindri").replace(/'/g, "'\\''")}'`);
    expect(execFileSync(path.join(bin, "sindri"), ["hello"], { encoding: "utf8" })).toBe("ran hello");
    expect(fs.statSync(path.join(bin, "sindri")).mode & 0o777).toBe(0o755);
    expect(wrapperTarget(d)).toBe(cli);
    expect(wrapperText("/b/sindri", "/n/node", "/c/cli.js")).toBe("#!/usr/bin/env bash\n# Written by sindri channel; change it with sindri channel promote or rollback.\nexport SINDRI_BIN='/b/sindri'\nexec '/n/node' '/c/cli.js' \"$@\"\n");
  });

  it("reads the target of an in-place wrapper too, and says nothing when there is none", () => {
    const d = deps();
    expect(wrapperTarget(d)).toBeNull();
    fs.writeFileSync(path.join(binDir(d), "sindri"), '#!/usr/bin/env bash\nexec "/usr/bin/node" "/repo/sindri/dist/cli.js" "$@"\n');
    expect(wrapperTarget(d)).toBe("/repo/sindri/dist/cli.js");
    fs.writeFileSync(path.join(binDir(d), "sindri"), "#!/bin/sh\necho hi\n");
    expect(wrapperTarget(d)).toBeNull();
    expect(binDir(makeDeps())).toMatch(/\.local\/bin$/);
  });
});

describe("promote and rollback", () => {
  const now = new Date("2026-10-10T00:00:00Z");

  it("promotes next, remembers the previous build, and rolls back and forth", async () => {
    const d = deps();
    const dirA = build(d, "stable", A);
    const dirB = build(d, "next", B);
    writeChannels(d, { stable: { ...entry(A, dirA, undefined, SCHEMA), previous: null }, next: entry(B, dirB, undefined, SCHEMA) });
    const smoke = fakeProc(() => ({}));
    const after = await promote(d, smoke, B, now);
    expect(smoke.calls).toEqual([{ argv: [process.execPath, path.join(dirB, "dist", "cli.js"), "--version"], cwd: dirB }]);
    expect(after.stable).toEqual({ sha: B, dir: dirB, installedAt: now.toISOString(), schema: SCHEMA, previous: entry(A, dirA, undefined, SCHEMA) });
    expect(readChannels(d)).toEqual(after);
    expect(wrapperTarget(d)).toBe(path.join(dirB, "dist", "cli.js"));
    const back = await rollback(d, smoke, now, SCHEMA);
    expect(back.stable).toEqual({ sha: A, dir: dirA, installedAt: now.toISOString(), schema: SCHEMA, previous: null });
    expect(wrapperTarget(d)).toBe(path.join(dirA, "dist", "cli.js"));
    // The build we rolled back from is not remembered: a second rollback refuses, going forward needs promote.
    await expect(rollback(d, smoke, now, SCHEMA)).rejects.toThrow(/no previous stable build/);
    expect(readChannels(d).stable?.sha).toBe(A);
  });

  it("promotes onto an empty stable", async () => {
    const d = deps();
    const dirB = build(d, "next", B);
    writeChannels(d, { stable: null, next: entry(B, dirB) });
    const after = await promote(d, fakeProc(() => ({})), B, now);
    expect(after.stable).toEqual({ sha: B, dir: dirB, installedAt: now.toISOString(), previous: null });
  });

  it("refuses the wrong sha, a missing build and a build that won't start, and leaves the wrapper and state alone", async () => {
    const d = deps();
    const dirA = build(d, "stable", A);
    const dirB = build(d, "next", B);
    const before: ChannelState = { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) };
    writeChannels(d, before);
    writeWrapper(d, path.join(dirA, "dist", "cli.js"));
    await expect(promote(d, fakeProc(() => ({})), C, now)).rejects.toThrow(`${C} is not what next runs`);
    await expect(promote(d, fakeProc(() => ({ code: 1 })), B, now)).rejects.toThrow(/the build at .* didn't start \(--version exited 1\)/);
    fs.rmSync(dirB, { recursive: true });
    await expect(promote(d, fakeProc(() => ({})), B, now)).rejects.toThrow(/doesn't exist/);
    expect(readChannels(d)).toEqual(before);
    expect(wrapperTarget(d)).toBe(path.join(dirA, "dist", "cli.js"));
    writeChannels(d, { stable: null, next: null });
    await expect(promote(d, fakeProc(() => ({})), B, now)).rejects.toThrow(/is not what next runs/);
  });

  it("keeps a previous build without a recorded schema as it was, so a later rollback can refuse it", async () => {
    const d = deps();
    const dirA = build(d, "stable", A);
    const dirB = build(d, "next", B);
    writeChannels(d, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB, undefined, SCHEMA) });
    const after = await promote(d, fakeProc(() => ({})), B, now);
    expect(after.stable?.previous).toEqual(entry(A, dirA));
    await expect(rollback(d, fakeProc(() => ({})), now, SCHEMA)).rejects.toThrow(/no recorded ledger schema/);
  });

  it("refuses to roll back without a previous build, or when that build is gone", async () => {
    const d = deps();
    await expect(rollback(d, fakeProc(() => ({})), now, SCHEMA)).rejects.toThrow(/no previous stable build/);
    const dirA = build(d, "stable", A);
    writeChannels(d, { stable: { ...entry(B, dirA), previous: null }, next: null });
    await expect(rollback(d, fakeProc(() => ({})), now, SCHEMA)).rejects.toThrow(/no previous stable build/);
    writeChannels(d, { stable: { ...entry(B, dirA), previous: entry(A, path.join(channelsRoot(d), "stable", "gone"), undefined, SCHEMA) }, next: null });
    await expect(rollback(d, fakeProc(() => ({})), now, SCHEMA)).rejects.toThrow(/doesn't exist/);
  });

  it("refuses to roll back to a build whose ledger schema is older than the current ledger, or unknown, and says why", async () => {
    const d = deps();
    const dirA = build(d, "stable", A);
    const dirB = build(d, "stable", B);
    const proc = fakeProc(() => ({}));
    writeChannels(d, { stable: { ...entry(B, dirB, undefined, SCHEMA + 1), previous: entry(A, dirA, undefined, SCHEMA) }, next: null });
    await expect(rollback(d, proc, now, SCHEMA + 1)).rejects.toThrow(`aaaaaaaa understands ledger schema v${SCHEMA}, but the ledger is at v${SCHEMA + 1}`);
    writeChannels(d, { stable: { ...entry(B, dirB, undefined, SCHEMA), previous: entry(A, dirA) }, next: null });
    await expect(rollback(d, proc, now, SCHEMA)).rejects.toThrow(/aaaaaaaa has no recorded ledger schema/);
    expect(proc.calls).toHaveLength(0);
    expect(wrapperTarget(d)).toBeNull();
    // An equal or newer schema is fine.
    writeChannels(d, { stable: { ...entry(B, dirB, undefined, SCHEMA), previous: entry(A, dirA, undefined, SCHEMA + 2) }, next: null });
    expect((await rollback(d, proc, now, SCHEMA)).stable?.sha).toBe(A);
  });
});
