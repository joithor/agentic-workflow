import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import {
  bumpEpoch, currentEpoch, LEDGER_SCHEMA_VERSION, ledgerPath, migrateWith, openLedger, openMemoryLedger, schemaVersion, withEpoch,
} from "../src/ledger/db.js";
import { getCursor, listEvents, listItems, markMissing, setCursor, upsertItem, type ObservedItem, type WriteCtx } from "../src/ledger/items.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { tempDir } from "./helpers.js";

const item = (over: Partial<ObservedItem> = {}): ObservedItem => ({
  id: "plan-x.t1", source: "plan-file", title: "Task one", state: "open", size: "S", sizedBy: "rules",
  ambiguity: "none", stepsDone: 0, stepsTotal: 5, contentHash: "h1", ...over,
});

function ctx(db: ReturnType<typeof openMemoryLedger>, now = "2026-10-08T12:00:00.000Z"): WriteCtx {
  return { epoch: currentEpoch(db), tickId: "tick-1", now: new Date(now), scrubber: makeScrubber() };
}

describe("ledger db", () => {
  it("creates a WAL ledger with private permissions and migrates idempotently", () => {
    const dir = path.join(tempDir(), "sindri");
    const file = ledgerPath(dir);
    expect(file).toBe(path.join(dir, "ledger.db"));
    const db = openLedger(file);
    expect(db.pragma("journal_mode", { simple: true })).toBe("wal");
    expect(schemaVersion(db)).toBe(LEDGER_SCHEMA_VERSION);
    expect(fs.statSync(file).mode & 0o777).toBe(0o600);
    expect(fs.statSync(dir).mode & 0o777).toBe(0o700);
    db.close();
    const again = openLedger(file);
    expect(schemaVersion(again)).toBe(LEDGER_SCHEMA_VERSION);
    again.close();
  });

  it("refuses a ledger written by a newer sindri (SND-LEDGER-001)", () => {
    const file = path.join(tempDir(), "ledger.db");
    const raw = new Database(file);
    raw.pragma("user_version = 99");
    raw.close();
    expect(() => openLedger(file)).toThrow(/SND-LEDGER-001|newer than this sindri/);
    try {
      openLedger(file);
    } catch (e) {
      expect((e as SindriError).code).toBe("SND-LEDGER-001");
    }
  });

  it("backs up an existing ledger before migrating it, and migrates idempotently", () => {
    const file = path.join(tempDir(), "ledger.db");
    const db = new Database(file);
    const steps = ["CREATE TABLE a (x INTEGER);", "CREATE TABLE b (y INTEGER);"];
    migrateWith(db, file, steps.slice(0, 1));
    expect(fs.existsSync(`${file}.bak-v1`)).toBe(false);
    migrateWith(db, file, steps);
    expect(fs.existsSync(`${file}.bak-v1`)).toBe(true);
    expect(schemaVersion(db)).toBe(2);
    migrateWith(db, file, steps);
    expect(schemaVersion(db)).toBe(2);
    db.close();
  });

  it("bumps the epoch and rejects writes under a stale one (SND-LOCK-003)", () => {
    const db = openMemoryLedger();
    expect(currentEpoch(db)).toBe(0);
    expect(bumpEpoch(db)).toBe(1);
    expect(withEpoch(db, 1, () => "ran")).toBe("ran");
    expect(bumpEpoch(db)).toBe(2);
    let ran = false;
    expect(() => withEpoch(db, 1, () => { ran = true; })).toThrow(SindriError);
    expect(ran).toBe(false);
  });
});

describe("ledger items", () => {
  it("records new, same and changed items with events", () => {
    const db = openMemoryLedger();
    bumpEpoch(db);
    expect(withEpoch(db, 1, () => upsertItem(db, ctx(db), item()))).toBe("new");
    expect(withEpoch(db, 1, () => upsertItem(db, ctx(db, "2026-10-08T13:00:00.000Z"), item()))).toBe("same");
    expect(withEpoch(db, 1, () => upsertItem(db, ctx(db), item({ contentHash: "h2", stepsDone: 2 })))).toBe("changed");
    expect(withEpoch(db, 1, () => upsertItem(db, ctx(db), item({ contentHash: "h3", state: "done", stepsDone: 5 })))).toBe("changed");
    const [row] = listItems(db);
    expect(row).toMatchObject({ id: "plan-x.t1", state: "done", steps_done: 5, last_seen: "2026-10-08T12:00:00.000Z", epoch: 1 });
    expect(listItems(db, { state: "open" })).toEqual([]);
    expect(listEvents(db, { itemId: "plan-x.t1" }).map((e) => e.kind)).toEqual(["seen", "changed", "state-changed"]);
    const stateEvent = listEvents(db, { itemId: "plan-x.t1" })[2];
    expect(JSON.parse(stateEvent.detail)).toEqual({ from: "open", to: "done" });
  });

  it("rejects a real write under a stale epoch and leaves no item or event", () => {
    const db = openMemoryLedger();
    const old = bumpEpoch(db);
    const stale = ctx(db);
    bumpEpoch(db);
    let caught: unknown;
    try {
      withEpoch(db, old, () => upsertItem(db, stale, item()));
    } catch (e) {
      caught = e;
    }
    expect((caught as SindriError).code).toBe("SND-LOCK-003");
    expect(listItems(db)).toEqual([]);
    expect(listEvents(db, {})).toEqual([]);
  });

  it("rolls back an upsert when the callback throws after it", () => {
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    expect(() =>
      withEpoch(db, epoch, () => {
        upsertItem(db, ctx(db), item());
        throw new Error("boom");
      }),
    ).toThrow("boom");
    expect(listItems(db)).toEqual([]);
    expect(listEvents(db, {})).toEqual([]);
  });

  it("closes items a full scan no longer returns", () => {
    const db = openMemoryLedger();
    bumpEpoch(db);
    withEpoch(db, 1, () => {
      upsertItem(db, ctx(db), item({ id: "p.t1", source: "plan-file:r" }));
      upsertItem(db, ctx(db), item({ id: "p.t2", source: "plan-file:r" }));
      upsertItem(db, ctx(db), item({ id: "q.t1", source: "plan-file:other" }));
    });
    expect(withEpoch(db, 1, () => markMissing(db, ctx(db), "plan-file:r", new Set(["p.t1"])))).toBe(1);
    expect(withEpoch(db, 1, () => markMissing(db, ctx(db), "plan-file:r", new Set(["p.t1"])))).toBe(0);
    expect(listItems(db).map((i) => [i.id, i.state])).toEqual([["q.t1", "open"], ["p.t1", "open"], ["p.t2", "removed"]]);
    expect(listEvents(db, { itemId: "p.t2" }).map((e) => e.kind)).toEqual(["seen", "removed"]);
  });

  it("scrubs and caps the stored title", () => {
    const db = openMemoryLedger();
    bumpEpoch(db);
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    withEpoch(db, 1, () => upsertItem(db, ctx(db), item({ title: `leak ${secret} ${"x".repeat(400)}` })));
    const [row] = listItems(db);
    expect(row.title).not.toContain(secret);
    expect(row.title.length).toBeLessThanOrEqual(200);
  });

  it("filters events by time and limit, and stores cursors", () => {
    const db = openMemoryLedger();
    bumpEpoch(db);
    withEpoch(db, 1, () => upsertItem(db, ctx(db, "2026-10-01T00:00:00.000Z"), item({ id: "a.t1" })));
    withEpoch(db, 1, () => upsertItem(db, ctx(db, "2026-10-08T00:00:00.000Z"), item({ id: "a.t2" })));
    expect(listEvents(db, { since: new Date("2026-10-05T00:00:00.000Z") }).map((e) => e.item_id)).toEqual(["a.t2"]);
    // The limit keeps the newest events, returned oldest first.
    expect(listEvents(db, { limit: 1 }).map((e) => e.item_id)).toEqual(["a.t2"]);
    expect(listEvents(db, {}).map((e) => e.item_id)).toEqual(["a.t1", "a.t2"]);
    expect(getCursor(db, "plan-file")).toBeNull();
    setCursor(db, "plan-file", "c1", new Date("2026-10-08T00:00:00.000Z"));
    setCursor(db, "plan-file", "c2", new Date("2026-10-08T01:00:00.000Z"));
    expect(getCursor(db, "plan-file")).toBe("c2");
  });
});

describe("ledger items are keyed by source and id (PR #69 review)", () => {
  it("keeps the same id from two sources apart, and markMissing touches only its own source", () => {
    const db = openMemoryLedger();
    bumpEpoch(db);
    withEpoch(db, 1, () => {
      expect(upsertItem(db, ctx(db), item({ id: "plan.t1", source: "plan-file:a", title: "A's task" }))).toBe("new");
      expect(upsertItem(db, ctx(db), item({ id: "plan.t1", source: "plan-file:b", title: "B's task" }))).toBe("new");
      expect(upsertItem(db, ctx(db), item({ id: "plan.t1", source: "plan-file:b", title: "B's task", contentHash: "h2" }))).toBe("changed");
    });
    expect(listItems(db).map((i) => [i.source, i.id, i.title, i.content_hash])).toEqual([
      ["plan-file:a", "plan.t1", "A's task", "h1"],
      ["plan-file:b", "plan.t1", "B's task", "h2"],
    ]);
    expect(withEpoch(db, 1, () => markMissing(db, ctx(db), "plan-file:a", new Set()))).toBe(1);
    expect(listItems(db).map((i) => [i.source, i.state])).toEqual([["plan-file:a", "removed"], ["plan-file:b", "open"]]);
    expect(listEvents(db, { itemId: "plan.t1" }).map((e) => [e.source, e.kind])).toEqual([
      ["plan-file:a", "seen"], ["plan-file:b", "seen"], ["plan-file:b", "changed"], ["plan-file:a", "removed"],
    ]);
  });
});
