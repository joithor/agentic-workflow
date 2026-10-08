import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";

import { SindriError } from "../errors.js";

export type Ledger = Database.Database;

// MIGRATIONS[i] moves the schema from version i to i+1 (PRAGMA user_version).
// Append only. Never edit an entry that has shipped. planning/ERD.md mirrors it.
// Item ids are unique only within a tracker source, so items are keyed by (source, id).
const MIGRATIONS: readonly string[] = [
  `
  CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
  INSERT INTO meta (key, value) VALUES ('epoch', '0');
  CREATE TABLE items (
    source TEXT NOT NULL,
    id TEXT NOT NULL,
    title TEXT NOT NULL,
    state TEXT NOT NULL,
    size TEXT,
    sized_by TEXT,
    ambiguity TEXT,
    steps_done INTEGER NOT NULL DEFAULT 0,
    steps_total INTEGER NOT NULL DEFAULT 0,
    content_hash TEXT NOT NULL,
    first_seen TEXT NOT NULL,
    last_seen TEXT NOT NULL,
    epoch INTEGER NOT NULL,
    PRIMARY KEY (source, id)
  );
  CREATE TABLE item_events (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL,
    item_id TEXT NOT NULL,
    ts TEXT NOT NULL,
    kind TEXT NOT NULL,
    detail TEXT NOT NULL,
    epoch INTEGER NOT NULL,
    tick_id TEXT NOT NULL,
    FOREIGN KEY (source, item_id) REFERENCES items(source, id)
  );
  CREATE INDEX item_events_item_ts ON item_events(item_id, ts);
  CREATE INDEX item_events_ts ON item_events(ts);
  CREATE TABLE profile_approvals (hash TEXT PRIMARY KEY, approved_at TEXT NOT NULL, approved_by TEXT NOT NULL);
  CREATE TABLE cursors (source TEXT PRIMARY KEY, cursor TEXT NOT NULL, updated_at TEXT NOT NULL);
  `,
];

export const LEDGER_SCHEMA_VERSION = MIGRATIONS.length;

export function schemaVersion(db: Ledger): number {
  return db.pragma("user_version", { simple: true }) as number;
}

// Exported with an injectable list so the backup path is testable before a
// second real migration exists. Two openers racing: the IMMEDIATE transaction
// re-reads the version, so the loser applies nothing.
export function migrateWith(db: Ledger, file: string | null, migrations: readonly string[]): void {
  const before = schemaVersion(db);
  if (before > migrations.length) {
    throw new SindriError("SND-LEDGER-001", `ledger schema v${before} is newer than this sindri (v${migrations.length})`);
  }
  if (before > 0 && before < migrations.length && file !== null) {
    db.pragma("wal_checkpoint(TRUNCATE)");
    fs.copyFileSync(file, `${file}.bak-v${before}`);
  }
  db.transaction(() => {
    for (let v = schemaVersion(db); v < migrations.length; v++) db.exec(migrations[v]);
    db.pragma(`user_version = ${migrations.length}`);
  }).immediate();
}

function migrate(db: Ledger, file: string | null): void {
  migrateWith(db, file, MIGRATIONS);
}

export function ledgerPath(stateDirPath: string): string {
  return path.join(stateDirPath, "ledger.db");
}

export function openLedger(file: string): Ledger {
  fs.mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  fs.chmodSync(path.dirname(file), 0o700);
  const db = new Database(file);
  db.pragma("journal_mode = WAL");
  db.pragma("foreign_keys = ON");
  db.pragma("busy_timeout = 5000");
  try {
    migrate(db, file);
  } catch (e) {
    db.close();
    throw e;
  }
  fs.chmodSync(file, 0o600);
  return db;
}

// Reads an existing ledger and leaves no files beside it (doctor, scrub). A
// readonly connection to a WAL ledger whose -wal file is gone creates -wal and
// -shm and can't remove them. This connection is query_only, so it writes
// nothing, and as the last connection it removes them on close. No migration,
// backup or chmod.
export function readLedger<T>(file: string, fn: (db: Ledger) => T): T {
  const db = new Database(file, { fileMustExist: true });
  try {
    db.pragma("query_only = ON");
    return fn(db);
  } finally {
    db.close();
  }
}

export function openMemoryLedger(): Ledger {
  const db = new Database(":memory:");
  db.pragma("foreign_keys = ON");
  migrate(db, null);
  return db;
}

export function currentEpoch(db: Ledger): number {
  const row = db.prepare("SELECT value FROM meta WHERE key = 'epoch'").get() as { value: string };
  return Number(row.value);
}

export function bumpEpoch(db: Ledger): number {
  return db
    .transaction(() => {
      const next = currentEpoch(db) + 1;
      db.prepare("UPDATE meta SET value = ? WHERE key = 'epoch'").run(String(next));
      return next;
    })
    .immediate();
}

// Fencing (spec §9.1): every write carries the epoch the writer acquired the
// lock under. A writer whose epoch is no longer current has been taken over:
// `fenced` then runs nothing and says so; `withEpoch` throws SND-LOCK-003.
export function fenced<T>(db: Ledger, epoch: number, fn: () => T): { ok: true; value: T } | { ok: false; current: number } {
  return db
    .transaction(() => {
      const cur = currentEpoch(db);
      return cur === epoch ? { ok: true as const, value: fn() } : { ok: false as const, current: cur };
    })
    .immediate();
}

export function withEpoch<T>(db: Ledger, epoch: number, fn: () => T): T {
  const r = fenced(db, epoch, fn);
  if (!r.ok) throw new SindriError("SND-LOCK-003", `stale epoch ${epoch} (current ${r.current}); another tick took over`);
  return r.value;
}
