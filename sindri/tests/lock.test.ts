import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SindriError } from "../src/errors.js";
import { currentEpoch, openMemoryLedger, withEpoch } from "../src/ledger/db.js";
import { acquireTickLock, inspectLock, type LockOwner } from "../src/lock/lock.js";
import { fakeSystem, tempDir } from "./helpers.js";

const now = () => new Date("2026-10-08T12:00:00.000Z");

function plantOwner(dir: string, owner: Partial<LockOwner>, raw?: string): string {
  const lock = path.join(dir, "sindri.lock");
  fs.mkdirSync(lock, { recursive: true });
  const full: LockOwner = { pid: 999, pidStartTime: "start-999", host: "test-host", bootId: "boot-1", startedAt: "2026-10-08T00:00:00.000Z", epoch: 7, ...owner };
  fs.writeFileSync(path.join(lock, "owner.json"), raw ?? JSON.stringify(full));
  return lock;
}

afterEach(() => vi.restoreAllMocks());

describe("acquireTickLock", () => {
  it("acquires a free lock, bumps the epoch and records it in owner.json", () => {
    const dir = tempDir();
    const db = openMemoryLedger();
    const r = acquireTickLock({ dir, db, sys: fakeSystem(), now });
    expect(r.ok).toBe(true);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.owner.epoch).toBe(1);
    const onDisk = JSON.parse(fs.readFileSync(path.join(dir, "sindri.lock", "owner.json"), "utf8"));
    expect(onDisk).toMatchObject({ pid: 4242, host: "test-host", bootId: "boot-1", epoch: 1 });
    expect(fs.readdirSync(dir).filter((n) => n.startsWith("lock.tmp-"))).toEqual([]);
  });

  it("respects a live owner, then succeeds after release", () => {
    const dir = tempDir();
    const db = openMemoryLedger();
    const a = acquireTickLock({ dir, db, sys: fakeSystem(), now });
    const b = acquireTickLock({ dir, db, sys: fakeSystem({ pid: 5555 }), now });
    expect(b.ok).toBe(false);
    expect(b.ok).toBe(false);
    if (!b.ok) expect(b.detail).toBe("locked by test-host/4242 since 2026-10-08T12:00:00.000Z");
    expect(a.ok).toBe(true);
    if (a.ok) a.release();
    expect(fs.existsSync(path.join(dir, "sindri.lock"))).toBe(false);
    const c = acquireTickLock({ dir, db, sys: fakeSystem({ pid: 5555 }), now });
    expect(c.ok && c.owner.epoch).toBe(2);
  });

  it.each([
    ["dead pid", fakeSystem({ pidAlive: (p) => p !== 999 })],
    ["different boot id", fakeSystem({ bootId: () => "boot-2" })],
    ["reused pid (start time differs)", fakeSystem({ pidStartTime: (p) => (p === 999 ? "start-other" : `start-${p}`) })],
  ])("takes over a stale lock: %s (Review Focus 1)", (_name, sys) => {
    const dir = tempDir();
    plantOwner(dir, {});
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys, now });
    expect(r.ok).toBe(true);
    expect(fs.readdirSync(dir).filter((n) => n.includes("stale-"))).toEqual([]);
  });

  it("never takes over another host's lock", () => {
    const dir = tempDir();
    plantOwner(dir, { host: "other-host" });
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: () => false }), now });
    expect(r.ok).toBe(false);
  });

  it.each(["{not json", "42"])("treats an unreadable owner (%s) as live for 60 s, then takes over", (raw) => {
    const dir = tempDir();
    const lock = plantOwner(dir, {}, raw);
    const fresh = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now: () => new Date() });
    expect(fresh.ok).toBe(false);
    expect(fresh.ok).toBe(false);
    if (!fresh.ok) expect(fresh.detail).toBe("locked by an unreadable owner (taken over after 60 s)");
    const old = new Date(Date.now() - 120_000);
    fs.utimesSync(lock, old, old);
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now: () => new Date() }).ok).toBe(true);
  });

  it("release does nothing when the lock is no longer ours", () => {
    const dir = tempDir();
    const a = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now });
    plantOwner(dir, { pid: 7777, startedAt: "later" });
    expect(a.ok).toBe(true);
    if (a.ok) a.release();
    expect(JSON.parse(fs.readFileSync(path.join(dir, "sindri.lock", "owner.json"), "utf8")).pid).toBe(7777);
  });

  it("the loser of a takeover race exits without the lock (Review Focus 2)", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    fs.mkdirSync(path.join(dir, "sindri.lock.takeover")); // another run is mid-takeover
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now: () => new Date() });
    expect(r.ok).toBe(false);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.detail).toBe("another run is taking over a stale lock");
    expect(fs.existsSync(path.join(dir, "sindri.lock", "owner.json"))).toBe(true);
  });

  it("clears a takeover dir left by a crashed taker, then the next run succeeds", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    const mutex = path.join(dir, "sindri.lock.takeover");
    fs.mkdirSync(mutex);
    const old = new Date(Date.now() - 120_000);
    fs.utimesSync(mutex, old, old);
    const sys = fakeSystem({ pidAlive: (p) => p !== 999 });
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys, now: () => new Date() }).ok).toBe(false);
    expect(fs.existsSync(mutex)).toBe(false);
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys, now: () => new Date() }).ok).toBe(true);
  });

  it("re-checks the owner under the takeover mutex and backs off when it changed", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {});
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p).endsWith("sindri.lock.takeover")) {
        fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 3333, pidStartTime: "start-3333", host: "test-host", bootId: "boot-1", startedAt: "new", epoch: 9 }));
      }
      return real(p, opts);
    });
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now });
    expect(r.ok).toBe(false);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.heldBy?.pid).toBe(3333);
    expect(JSON.parse(fs.readFileSync(path.join(lock, "owner.json"), "utf8")).pid).toBe(3333);
  });

  it("puts back a lock that is no longer the dead owner's after the rename and exits (spec §9.1 M2)", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {});
    const db = openMemoryLedger();
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(from) === lock && String(to).includes(".stale-")) {
        // A new live owner replaced the dead one after our pre-rename check.
        fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 3333, pidStartTime: "start-3333", host: "test-host", bootId: "boot-1", startedAt: "new", epoch: 9 }));
      }
      real(from, to);
    });
    const r = acquireTickLock({ dir, db, sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.heldBy?.pid).toBe(3333);
    expect(JSON.parse(fs.readFileSync(path.join(lock, "owner.json"), "utf8")).pid).toBe(3333);
    expect(fs.readdirSync(dir).filter((n) => n.includes("stale-"))).toEqual([]);
    expect(currentEpoch(db)).toBe(0);
  });

  it("exits as a no-op when the lock changed hands mid-takeover, even if the new owner is dead too (M2)", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {});
    const db = openMemoryLedger();
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(from) === lock && String(to).includes(".stale-")) {
        fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 3333, pidStartTime: "start-3333", host: "test-host", bootId: "boot-1", startedAt: "new", epoch: 9 }));
      }
      real(from, to);
    });
    // Looping would take over 3333's (dead) lock on the next pass; M2 says exit.
    const r = acquireTickLock({ dir, db, sys: fakeSystem({ pidAlive: (p) => p !== 999 && p !== 3333 }), now });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.detail).toBe("the lock changed hands during the takeover; left it to its new owner");
    expect(JSON.parse(fs.readFileSync(path.join(lock, "owner.json"), "utf8")).pid).toBe(3333);
    expect(currentEpoch(db)).toBe(0);
    expect(fs.existsSync(path.join(dir, "sindri.lock.takeover"))).toBe(false);
  });

  it("reports a no-op, and leaves the moved lock in place, when the put-back target is occupied", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {});
    const db = openMemoryLedger();
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(from) === lock && String(to).includes(".stale-")) {
        fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 3333, pidStartTime: "start-3333", host: "test-host", bootId: "boot-1", startedAt: "new", epoch: 9 }));
      }
      if (String(from).includes(".stale-") && String(to) === lock) plantOwner(dir, { pid: 7777, startedAt: "e" }); // acquirer E got in between
      real(from, to);
    });
    const r = acquireTickLock({ dir, db, sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now });
    expect(r.ok).toBe(false);
    const stale = fs.readdirSync(dir).filter((n) => n.includes(".stale-"));
    expect(stale).toHaveLength(1);
    if (!r.ok) {
      expect(r.detail).toBe(`moved a live lock aside and could not restore it; it is at ${path.join(dir, stale[0])}`);
      expect(r.heldBy?.pid).toBe(3333);
    }
    expect(JSON.parse(fs.readFileSync(path.join(dir, stale[0], "owner.json"), "utf8")).pid).toBe(3333);
    expect(JSON.parse(fs.readFileSync(path.join(lock, "owner.json"), "utf8")).pid).toBe(7777);
    expect(currentEpoch(db)).toBe(0);
    expect(inspectLock(dir, fakeSystem(), now).leftovers).toEqual(stale);
  });

  it("never clears a takeover mutex whose taker is alive, however old", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    const mutex = plantOwner(path.join(dir, "m"), { pid: 6000, pidStartTime: "start-6000", startedAt: "taker" });
    fs.renameSync(mutex, path.join(dir, "sindri.lock.takeover"));
    const old = new Date(Date.now() - 600_000);
    fs.utimesSync(path.join(dir, "sindri.lock.takeover"), old, old);
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now: () => new Date() });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.detail).toBe("another run is taking over a stale lock");
    expect(fs.existsSync(path.join(dir, "sindri.lock.takeover", "owner.json"))).toBe(true);
    // Once that taker is provably dead, its mutex is cleared.
    const sys = fakeSystem({ pidAlive: (p) => p !== 999 && p !== 6000 });
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys, now: () => new Date() }).ok).toBe(false);
    expect(fs.existsSync(path.join(dir, "sindri.lock.takeover"))).toBe(false);
  });

  it("removes the takeover mutex only while it is still its own", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {});
    const mutex = path.join(dir, "sindri.lock.takeover");
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      // Another taker replaced our mutex while we paused.
      if (String(from) === lock && String(to).includes(".stale-")) fs.writeFileSync(path.join(mutex, "owner.json"), JSON.stringify({ pid: 8888, pidStartTime: null, host: "test-host", bootId: null, startedAt: "c", epoch: 0 }));
      real(from, to);
    });
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now }).ok).toBe(true);
    expect(JSON.parse(fs.readFileSync(path.join(mutex, "owner.json"), "utf8")).pid).toBe(8888);
  });

  it("rethrows an unexpected error creating the takeover mutex", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    const real = fs.mkdirSync;
    vi.spyOn(fs, "mkdirSync").mockImplementation((p, opts) => {
      if (String(p).endsWith("sindri.lock.takeover")) throw Object.assign(new Error("denied"), { code: "EACCES" });
      return real(p, opts);
    });
    expect(() => acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now })).toThrow("denied");
  });

  it("gives up after three attempts that can't move the dead lock", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to).includes(".stale-")) throw Object.assign(new Error("gone"), { code: "ENOENT" });
      real(from, to);
    });
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidAlive: (p) => p !== 999 }), now });
    expect(r.ok).toBe(false);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.detail).toBe("lost the takeover race");
  });

  it("retries when the lock vanishes between the failed rename and the check", () => {
    const dir = tempDir();
    const lock = plantOwner(dir, {}, "{not json");
    const real = fs.renameSync;
    let first = true;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (first && String(to) === lock) {
        first = false;
        fs.rmSync(lock, { recursive: true });
        throw Object.assign(new Error("exists"), { code: "ENOTEMPTY" });
      }
      real(from, to);
    });
    expect(acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now }).ok).toBe(true);
  });

  it("removes its lock if the epoch can't be bumped", () => {
    const dir = tempDir();
    const db = openMemoryLedger();
    db.close();
    expect(() => acquireTickLock({ dir, db, sys: fakeSystem(), now })).toThrow();
    expect(fs.existsSync(path.join(dir, "sindri.lock"))).toBe(false);
  });

  it("release puts back a lock that turned out not to be its own", () => {
    const dir = tempDir();
    const a = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now });
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to).includes(".released-")) {
        fs.writeFileSync(path.join(String(from), "owner.json"), JSON.stringify({ pid: 6666, pidStartTime: "start-6666", host: "test-host", bootId: "boot-1", startedAt: "x", epoch: 3 }));
      }
      real(from, to);
    });
    expect(a.ok).toBe(true);
    if (a.ok) a.release();
    expect(JSON.parse(fs.readFileSync(path.join(dir, "sindri.lock", "owner.json"), "utf8")).pid).toBe(6666);
  });

  it("release is a no-op when its lock dir is already gone", () => {
    const dir = tempDir();
    const a = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now });
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to).includes(".released-")) throw Object.assign(new Error("gone"), { code: "ENOENT" });
      real(from, to);
    });
    expect(a.ok).toBe(true);
    if (a.ok) expect(() => a.release()).not.toThrow();
  });

  it("treats an unknown start time as no evidence (a live owner is kept)", () => {
    const dir = tempDir();
    plantOwner(dir, {});
    const r = acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem({ pidStartTime: (p) => (p === 999 ? null : `start-${p}`) }), now });
    expect(r.ok).toBe(false);
  });

  it("rethrows unexpected filesystem errors", () => {
    const dir = tempDir();
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to) === path.join(dir, "sindri.lock")) throw Object.assign(new Error("denied"), { code: "EACCES" });
      real(from, to);
    });
    expect(() => acquireTickLock({ dir, db: openMemoryLedger(), sys: fakeSystem(), now })).toThrow("denied");
  });

  it("a taken-over writer's epoch is rejected (fencing)", () => {
    const dir = tempDir();
    const db = openMemoryLedger();
    const a = acquireTickLock({ dir, db, sys: fakeSystem({ pid: 1 }), now });
    const b = acquireTickLock({ dir, db, sys: fakeSystem({ pid: 2, pidAlive: (p) => p !== 1 }), now });
    expect(a.ok && b.ok).toBe(true);
    expect(a.ok).toBe(true);
    if (!a.ok) return;
    expect(() => withEpoch(db, a.owner.epoch, () => 0)).toThrow(SindriError);
  });
});

describe("inspectLock", () => {
  it("reports free, held, stale and leftovers", () => {
    const dir = tempDir();
    expect(inspectLock(dir, fakeSystem(), now)).toEqual({ state: "free", owner: null, leftovers: [] });
    plantOwner(dir, {});
    expect(inspectLock(dir, fakeSystem(), now).state).toBe("held");
    expect(inspectLock(dir, fakeSystem({ pidAlive: () => false }), now).state).toBe("stale");
    fs.mkdirSync(path.join(dir, "sindri.lock.stale-abc"));
    expect(inspectLock(dir, fakeSystem(), now).leftovers).toEqual(["sindri.lock.stale-abc"]);
    expect(inspectLock(path.join(dir, "missing"), fakeSystem(), now).state).toBe("free");
  });

  it("falls back to pid liveness when boot id or start time is unknown", () => {
    const dir = tempDir();
    plantOwner(dir, { bootId: null, pidStartTime: null });
    expect(inspectLock(dir, fakeSystem(), now).state).toBe("held");
    expect(inspectLock(dir, fakeSystem({ bootId: () => null, pidAlive: () => false }), now).state).toBe("stale");
  });
});
