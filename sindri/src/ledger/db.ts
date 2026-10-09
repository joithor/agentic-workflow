import { randomBytes } from "node:crypto";
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
  `
  CREATE TABLE shape_runs (
    run_id TEXT PRIMARY KEY,
    repo TEXT NOT NULL,
    ts TEXT NOT NULL,
    head TEXT,
    tree TEXT,
    commit_sha TEXT,
    elapsed_ms INTEGER NOT NULL,
    index_age_ms INTEGER,
    providers TEXT NOT NULL,
    parser TEXT NOT NULL,
    deferred TEXT NOT NULL,
    signal_count INTEGER NOT NULL,
    epoch INTEGER NOT NULL,
    closed_at TEXT
  );
  CREATE INDEX shape_runs_pending ON shape_runs(commit_sha, closed_at, ts);
  CREATE INDEX shape_runs_tree ON shape_runs(repo, tree, parser, ts, run_id);
  CREATE TABLE shape_signals (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL REFERENCES shape_runs(run_id),
    type TEXT NOT NULL,
    layer TEXT NOT NULL,
    value REAL NOT NULL,
    threshold REAL NOT NULL,
    at TEXT NOT NULL,
    existing TEXT,
    detail TEXT NOT NULL,
    name TEXT,
    ast_hash TEXT,
    outcome TEXT,
    labeled_at TEXT,
    epoch INTEGER NOT NULL
  );
  CREATE INDEX shape_signals_type ON shape_signals(type);
  CREATE INDEX shape_signals_run ON shape_signals(run_id);
  `,
  `
  CREATE TABLE scope_runs (
    run_id TEXT PRIMARY KEY,
    subject TEXT NOT NULL,
    mode TEXT NOT NULL,
    ts TEXT NOT NULL,
    status TEXT NOT NULL,
    rounds INTEGER NOT NULL,
    surfaces INTEGER NOT NULL,
    recall REAL,
    precision REAL,
    baseline_recall REAL,
    baseline_precision REAL,
    leaky INTEGER NOT NULL DEFAULT 0,
    tokens INTEGER NOT NULL,
    out_path TEXT NOT NULL,
    epoch INTEGER NOT NULL
  );
  CREATE TABLE model_calls (
    run_id TEXT NOT NULL REFERENCES scope_runs(run_id),
    seq INTEGER NOT NULL,
    role TEXT NOT NULL,
    model TEXT NOT NULL,
    input_tokens INTEGER NOT NULL,
    output_tokens INTEGER NOT NULL,
    PRIMARY KEY (run_id, seq)
  );
  `,
];

export const LEDGER_SCHEMA_VERSION = MIGRATIONS.length;

export function schemaVersion(db: Ledger): number {
  return db.pragma("user_version", { simple: true }) as number;
}

// Exported with an injectable list so the backup path is testable before a
// second real migration exists. Two openers racing: the IMMEDIATE transaction
// re-reads the version, so the loser applies nothing. The backup is `VACUUM INTO`: one
// read transaction, so a consistent copy with the WAL's newest frames whatever other
// connections hold open (a checkpoint plus a file copy misses frames a reader pins and can
// tear). It is the synchronous form of better-sqlite3's async db.backup(), which openLedger
// can't await. Written to a unique temp name, then renamed: VACUUM INTO refuses an
// existing file, and a racing opener's backup must not fail this one.
export function migrateWith(db: Ledger, file: string | null, migrations: readonly string[]): void {
  const before = schemaVersion(db);
  if (before > migrations.length) {
    throw new SindriError("SND-LEDGER-001", `ledger schema v${before} is newer than this sindri (v${migrations.length})`);
  }
  if (before > 0 && before < migrations.length && file !== null) {
    const bak = `${file}.bak-v${before}`;
    const tmp = `${bak}.tmp-${process.pid}-${randomBytes(4).toString("hex")}`;
    db.prepare("VACUUM INTO ?").run(tmp);
    fs.renameSync(tmp, bak);
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

// The pre-commit hook's open (spec §5.2: hooks never write the ledger): no migration, no
// WAL switch. Not `readonly: true`: that creates -wal/-shm beside a WAL ledger and can't
// remove them (see readLedger); query_only writes nothing. A missing file or a schema
// version this build doesn't know is null.
export function openLedgerReadOnly(file: string): Ledger | null {
  if (!fs.existsSync(file)) return null;
  const db = new Database(file, { fileMustExist: true });
  db.pragma("query_only = ON");
  if (schemaVersion(db) !== LEDGER_SCHEMA_VERSION) {
    db.close();
    return null;
  }
  return db;
}

// The schema version a ledger file is at, or null when there is no file: tells a hook why
// openLedgerReadOnly answered null. Read-only, like it.
export function ledgerFileVersion(file: string): number | null {
  if (!fs.existsSync(file)) return null;
  const db = new Database(file, { fileMustExist: true });
  try {
    db.pragma("query_only = ON");
    return schemaVersion(db);
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
