import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { awStateDir } from "../src/deps.js";
import { SindriError } from "../src/errors.js";
import { heavyLockDir, heavyLockState, withHeavyLock } from "../src/index/heavy-lock.js";
import { fakeSystem, makeDeps } from "./helpers.js";

afterEach(() => vi.restoreAllMocks());

const LOCKS_SH = path.resolve(import.meta.dirname, "../../config/lib/locks.sh");
const holderPath = (root: string): string => `${heavyLockDir(root)}.holder.json`;

describe("heavy-job lock", () => {
  it("holds the lock for the duration of the job and records the holder", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const seen = await withHeavyLock(d, "index-build", 0, async () => heavyLockState(root, d.now));
    expect(seen).toMatchObject({ held: true, holder: { kind: "index-build", pid: 4242, host: "test-host", reclaimed: null } });
    expect(heavyLockState(root, d.now)).toEqual({ held: false, holder: null, ageMs: null });
  });

  it("puts the lock at locks/heavy-job.lock, or at $AW_HEAVY_JOB_LOCK, with the holder beside it", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    expect(heavyLockDir(root)).toBe(path.join(root, "locks", "heavy-job.lock"));
    const custom = path.join(root, "elsewhere", "my.lock");
    const d2 = makeDeps({ env: { ...d.env, AW_HEAVY_JOB_LOCK: custom } });
    expect(heavyLockDir(root, d2.env)).toBe(custom);
    await withHeavyLock(d2, "index-build", 0, async () => {
      expect(fs.readdirSync(custom)).toEqual([]);
      expect(fs.existsSync(`${custom}.holder.json`)).toBe(true);
    });
    expect(fs.existsSync(`${custom}.holder.json`)).toBe(false);
  });

  it("is the same lock as config/lib/locks.sh", async () => {
    const d = makeDeps();
    const dir = heavyLockDir(awStateDir(d));
    await withHeavyLock(d, "index-build", 0, async () => {
      const r = execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0 && echo got || echo busy`], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
      expect(r.trim()).toBe("busy");
    });
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; acquire_lock "${dir}" 0`]);
    expect(fs.existsSync(dir)).toBe(true);
    await expect(withHeavyLock(d, "index-build", 0, async () => 1)).rejects.toThrow(SindriError);
    execFileSync("bash", ["-c", `source "${LOCKS_SH}"; release_lock "${dir}"`]);
    expect(await withHeavyLock(d, "index-build", 0, async () => 2)).toBe(2);
  });

  it("waits, logs the holder once, then reports it when still busy", async () => {
    let slept = 0;
    const logs: string[] = [];
    const d = makeDeps({ sleep: async () => { slept++; }, log: (l) => logs.push(l) });
    const root = awStateDir(d);
    await withHeavyLock(d, "test-suite", 0, async () => {
      const err = await withHeavyLock(d, "index-build", 2000, async () => 0).catch((e: unknown) => e);
      expect(err).toBeInstanceOf(SindriError);
      expect((err as SindriError).code).toBe("SND-INDEX-001");
      expect((err as SindriError).message).toContain("test-suite, pid 4242");
    });
    expect(slept).toBe(2);
    expect(logs).toEqual([expect.stringContaining("waiting for the heavy-job lock (test-suite, pid 4242")]);
    fs.mkdirSync(heavyLockDir(root));
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toBe("the heavy-job lock is busy");
    fs.writeFileSync(holderPath(root), JSON.stringify({ nope: 1 }));
    const err2 = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err2 as SindriError).message).toBe("the heavy-job lock is busy");
    expect(heavyLockState(root, () => new Date(Date.now() + 60_000)).ageMs).toBeGreaterThan(0);
  });

  it("reclaims a lock whose holder pid is dead on this host (and says so), but not a live pid or another host's", async () => {
    const logs: string[] = [];
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const plant = (holder: object): void => {
      fs.mkdirSync(heavyLockDir(root), { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify(holder));
    };
    plant({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" });
    const seen = await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now).holder);
    expect(seen).toMatchObject({ kind: "next", reclaimed: "dead pid 999 (index-build)" });
    expect(logs).toEqual(["reclaiming the heavy-job lock left by dead pid 999 (index-build)"]);
    plant({ kind: "index-build", pid: 4242, host: "test-host", startedAt: "t" });
    await expect(withHeavyLock(d, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 4242");
    plant({ kind: "index-build", pid: 999, host: "other-host", startedAt: "t" });
    await expect(withHeavyLock(d, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
  });

  it("releases the lock when the job throws, or when the holder record can't be written", async () => {
    const d = makeDeps();
    await expect(withHeavyLock(d, "x", 0, async () => { throw new Error("job failed"); })).rejects.toThrow("job failed");
    expect(heavyLockState(awStateDir(d), d.now).held).toBe(false);
    vi.spyOn(fs, "writeFileSync").mockImplementationOnce(() => { throw new Error("disk full"); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("disk full");
    expect(heavyLockState(awStateDir(d), d.now).held).toBe(false);
  });

  it("rethrows unexpected mkdir errors", async () => {
    const d = makeDeps();
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p) === heavyLockDir(awStateDir(d))) throw Object.assign(new Error("no space left"), { code: "ENOSPC" });
      return real(p, opts);
    });
    await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow("no space left");
  });
});
