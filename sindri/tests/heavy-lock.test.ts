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
const holderPath = (root: string): string => `${heavyLockDir(root, {})}.holder.json`;

describe("heavy-job lock", () => {
  it("holds the lock for the duration of the job and records the holder", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const seen = await withHeavyLock(d, "index-build", 0, async () => heavyLockState(root, d.now, d.env));
    expect(seen).toMatchObject({ held: true, holder: { kind: "index-build", pid: 4242, host: "test-host", reclaimed: null } });
    expect(heavyLockState(root, d.now, d.env)).toEqual({ held: false, holder: null, ageMs: null });
  });

  it("puts the lock at locks/heavy-job.lock, or at $AW_HEAVY_JOB_LOCK, with the holder beside it", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    expect(heavyLockDir(root, {})).toBe(path.join(root, "locks", "heavy-job.lock"));
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
    const dir = heavyLockDir(awStateDir(d), d.env);
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
    fs.mkdirSync(heavyLockDir(root, d.env));
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy; held \d+ (min|h)$/);
    fs.writeFileSync(holderPath(root), JSON.stringify({ nope: 1 }));
    const err2 = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err2 as SindriError).message).toMatch(/^the heavy-job lock is busy; held /);
    expect(heavyLockState(root, () => new Date(Date.now() + 60_000), d.env).ageMs).toBeGreaterThan(0);
  });

  it("reclaims a lock whose holder pid is dead on this host (and says so), but not a live pid or another host's", async () => {
    const logs: string[] = [];
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }), log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const plant = (holder: object): void => {
      fs.mkdirSync(heavyLockDir(root, d.env), { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify(holder));
    };
    plant({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" });
    const seen = await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env).holder);
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
    expect(heavyLockState(awStateDir(d), d.now, d.env).held).toBe(false);
    vi.spyOn(fs, "writeFileSync").mockImplementationOnce(() => { throw new Error("disk full"); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("disk full");
    expect(heavyLockState(awStateDir(d), d.now, d.env).held).toBe(false);
  });

  it("rethrows unexpected mkdir errors", async () => {
    const d = makeDeps();
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p) === heavyLockDir(awStateDir(d), d.env)) throw Object.assign(new Error("no space left"), { code: "ENOSPC" });
      return real(p, opts);
    });
    await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow("no space left");
  });

  it("says busy without an age when the lock is released between the failed mkdir and the check", async () => {
    const d = makeDeps();
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p) === heavyLockDir(awStateDir(d), d.env)) throw Object.assign(new Error("exists"), { code: "EEXIST" });
      return real(p, opts);
    });
    await expect(withHeavyLock(d, "x", 0, async () => 0)).rejects.toThrow(/^the heavy-job lock is busy$/);
  });

  it("records the boot id and treats a holder from another boot as dead (a reused pid looks alive)", async () => {
    const d = makeDeps();
    const root = awStateDir(d);
    const plant = (holder: object): void => {
      fs.mkdirSync(heavyLockDir(root, d.env), { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify(holder));
    };
    expect(await withHeavyLock(d, "x", 0, async () => heavyLockState(root, d.now, d.env).holder?.bootId)).toBe("boot-1");
    plant({ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-0", startedAt: "t" });
    expect(await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env).holder?.reclaimed)).toBe("dead pid 777 (index-build)");
    // Same boot, or a record (or a host) that can't say which boot: the live pid holds it.
    for (const [holder, sys] of [
      [{ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-1", startedAt: "t" }, fakeSystem()],
      [{ kind: "index-build", pid: 777, host: "test-host", startedAt: "t" }, fakeSystem()],
      [{ kind: "index-build", pid: 777, host: "test-host", bootId: "boot-0", startedAt: "t" }, fakeSystem({ bootId: () => null })],
    ] as const) {
      plant(holder);
      await expect(withHeavyLock({ ...d, system: sys }, "next", 0, async () => 1)).rejects.toThrow("index-build, pid 777");
      fs.rmSync(holderPath(root));
      fs.rmdirSync(heavyLockDir(root, d.env));
    }
  });

  it("reclaims a lock held over 6 h as a last resort: no holder record (a crashed locks.sh holder) or a reused live pid", async () => {
    const logs: string[] = [];
    const d = makeDeps({ log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const age = (hours: number): void => {
      const t = new Date(d.now().getTime() - hours * 3_600_000);
      fs.utimesSync(dir, t, t);
    };
    fs.mkdirSync(dir, { recursive: true });
    age(1);
    const busy = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((busy as SindriError).message).toBe("the heavy-job lock is busy; held 1 h");
    age(7);
    expect(await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env).holder?.reclaimed)).toBe("a lock held over 6 h with no holder record");
    fs.mkdirSync(dir);
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 4242, host: "test-host", startedAt: "t" }));
    const t = new Date(d.now().getTime() - 7 * 3_600_000);
    fs.utimesSync(holderPath(root), t, t);
    age(7);
    expect(await withHeavyLock(d, "next", 0, async () => heavyLockState(root, d.now, d.env).holder?.reclaimed)).toBe("a lock held over 6 h by index-build (pid 4242)");
    expect(logs).toEqual([
      "reclaiming the heavy-job lock left by a lock held over 6 h with no holder record",
      "reclaiming the heavy-job lock left by a lock held over 6 h by index-build (pid 4242)",
    ]);
  });

  it("ignores a holder record older than the lock dir (a leftover beside a lock taken by locks.sh)", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    const old = new Date(fs.statSync(dir).mtimeMs - 60_000);
    fs.utimesSync(holderPath(root), old, old);
    const err = await withHeavyLock(d, "x", 0, async () => 0).catch((e: unknown) => e);
    expect((err as SindriError).message).toMatch(/^the heavy-job lock is busy; held /);
    expect(fs.existsSync(dir)).toBe(true);
    expect(heavyLockState(root, d.now, d.env).holder).toBeNull();
  });

  it("lets exactly one of two waiters on the same dead holder take the lock over", async () => {
    const dead = fakeSystem({ pidAlive: (p) => p !== 999 });
    const a = makeDeps({ system: dead });
    const b = { ...a, system: { ...dead, pid: 5151 } };
    const root = awStateDir(a);
    fs.mkdirSync(heavyLockDir(root, a.env), { recursive: true });
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    const entered: string[] = [];
    let open!: () => void;
    const gate = new Promise<void>((r) => { open = r; });
    let pB: Promise<void> | null = null;
    const real = fs.readFileSync;
    // A has read the dead holder; before it acts, B runs its whole takeover (up to its job's first await).
    vi.spyOn(fs, "readFileSync").mockImplementation(((p: fs.PathOrFileDescriptor, o?: unknown) => {
      const text = real(p, o as BufferEncoding);
      if (String(p) === holderPath(root) && pB === null) {
        pB = Promise.resolve();
        pB = withHeavyLock(b, "b", 0, async () => { entered.push("b"); await gate; });
      }
      return text;
    }) as typeof fs.readFileSync);
    const errA = await withHeavyLock(a, "a", 0, async () => { entered.push("a"); }).catch((e: unknown) => e);
    expect((errA as SindriError).code).toBe("SND-INDEX-001");
    expect((errA as SindriError).message).toContain("(b, pid 5151");
    expect(entered).toEqual(["b"]);
    expect(heavyLockState(root, a.now, a.env).holder).toMatchObject({ kind: "b", pid: 5151, reclaimed: "dead pid 999 (index-build)" });
    open();
    await pB;
    expect(heavyLockState(root, a.now, a.env).held).toBe(false);
    expect(fs.readdirSync(path.dirname(heavyLockDir(root, a.env)))).toEqual([]);
  });

  it("leaves a live taker's reclaim mutex alone and clears a dead or long-unreadable one", async () => {
    let slept = 0;
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 && p !== 998 }), sleep: async () => { slept++; } });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const mutex = `${dir}.reclaim`;
    const plant = (taker: object | null, ageMs = 0): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
      fs.mkdirSync(mutex, { recursive: true });
      if (taker !== null) fs.writeFileSync(path.join(mutex, "holder.json"), JSON.stringify(taker));
      const t = new Date(d.now().getTime() - ageMs);
      fs.utimesSync(mutex, t, t);
    };
    plant({ kind: "index-build", pid: 31, host: "test-host", startedAt: "t" });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
    expect(fs.existsSync(mutex)).toBe(true);
    plant(null, 1000);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
    expect(fs.existsSync(mutex)).toBe(true);
    fs.rmSync(mutex, { recursive: true });
    for (const [taker, age] of [[{ kind: "index-build", pid: 998, host: "test-host", startedAt: "t" }, 0], [null, 61_000]] as const) {
      plant(taker, age);
      expect(await withHeavyLock(d, "x", 1000, async () => 2)).toBe(2);
      expect(fs.existsSync(mutex)).toBe(false);
    }
    expect(slept).toBe(2);
    // The mutex vanished between the failed mkdir and its age check: just try again.
    plant(null);
    const realStat = fs.statSync;
    vi.spyOn(fs, "statSync").mockImplementation(((p: fs.PathLike, o?: fs.StatSyncOptions) => (String(p) === mutex ? undefined : realStat(p, o))) as typeof fs.statSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("index-build, pid 999");
  });

  it("backs off when the dead holder's lock is released, moved or replaced during the takeover", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const plant = (): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    };
    const realMkdir = fs.mkdirSync;
    const realRename = fs.renameSync;
    const onMutex = (act: () => void): void => {
      vi.spyOn(fs, "mkdirSync").mockImplementation(((p: fs.PathLike, o?: fs.MakeDirectoryOptions) => {
        if (String(p) === `${dir}.reclaim`) act();
        return realMkdir(p, o);
      }) as typeof fs.mkdirSync);
    };
    // Released (by a human) before the mutex: the recheck finds no dir.
    plant();
    onMutex(() => { fs.rmSync(holderPath(root), { force: true }); if (fs.existsSync(dir)) fs.rmdirSync(dir); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    // The rename finds the dir gone.
    plant();
    vi.spyOn(fs, "renameSync").mockImplementationOnce(() => { throw Object.assign(new Error("gone"), { code: "ENOENT" }); });
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    vi.restoreAllMocks();
    // The dir was replaced between the recheck and the rename: put the new one back.
    fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      fs.rmdirSync(from);
      realMkdir(from);
      realRename(from, to);
    }) as typeof fs.renameSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    expect(fs.existsSync(dir)).toBe(true);
    expect(fs.readdirSync(path.dirname(dir)).filter((n) => n.includes(".stale-"))).toEqual([]);
    vi.restoreAllMocks();
    fs.rmdirSync(dir);
    // A waiter that wasn't reclaiming took the freed dir first.
    plant();
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      realRename(from, to);
      realMkdir(from);
    }) as typeof fs.renameSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("the heavy-job lock is busy");
    expect(fs.existsSync(dir)).toBe(true);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(false);
  });

  it("keeps the mutex until the holder is written, frees the dir when that write fails, and never removes another taker's mutex", async () => {
    const d = makeDeps({ system: fakeSystem({ pidAlive: (p) => p !== 999 }) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    const plant = (): void => {
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "index-build", pid: 999, host: "test-host", startedAt: "t" }));
    };
    plant();
    const realWrite = fs.writeFileSync;
    vi.spyOn(fs, "writeFileSync").mockImplementation(((p: fs.PathOrFileDescriptor, data: string) => {
      if (String(p) === holderPath(root)) {
        expect(fs.existsSync(`${dir}.reclaim`)).toBe(true);
        throw new Error("disk full");
      }
      realWrite(p, data);
    }) as typeof fs.writeFileSync);
    await expect(withHeavyLock(d, "x", 0, async () => 1)).rejects.toThrow("disk full");
    expect(fs.existsSync(dir)).toBe(false);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(false);
    vi.restoreAllMocks();
    // Our mutex was cleared and retaken by another taker mid-reclaim: leave theirs.
    plant();
    const realRename = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementationOnce(((from: fs.PathLike, to: fs.PathLike) => {
      fs.rmSync(`${dir}.reclaim`, { recursive: true });
      fs.mkdirSync(`${dir}.reclaim`);
      realRename(from, to);
    }) as typeof fs.renameSync);
    expect(await withHeavyLock(d, "x", 0, async () => 3)).toBe(3);
    expect(fs.existsSync(`${dir}.reclaim`)).toBe(true);
  });

  it("releases only its own lock, and a failed release never masks the job's result", async () => {
    const logs: string[] = [];
    const d = makeDeps({ log: (l) => logs.push(l) });
    const root = awStateDir(d);
    const dir = heavyLockDir(root, d.env);
    // Someone else's holder now names the lock: leave it to them.
    await withHeavyLock(d, "x", 0, async () => {
      fs.writeFileSync(holderPath(root), JSON.stringify({ kind: "other", pid: 31, host: "test-host", startedAt: "t2" }));
    });
    expect(heavyLockState(root, d.now, d.env).holder).toMatchObject({ kind: "other" });
    fs.rmSync(holderPath(root));
    fs.rmdirSync(dir);
    expect(await withHeavyLock(d, "x", 0, async () => { fs.rmdirSync(dir); return 7; })).toBe(7);
    expect(logs).toEqual([expect.stringContaining(`could not release the heavy-job lock ${dir}`)]);
  });
});
