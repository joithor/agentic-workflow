import { layers, openIndexReadOnly, type IndexDb } from "./db.js";

// Whether a code index can be read, shared by `sindri doctor` and the publish gate's identifiers layer, so the two
// never disagree about an index. Only damage counts as damage: SQLite's "no such table", and a file that isn't a
// database or is corrupt (by error code, not message). Every other error (SQLITE_BUSY, a locked file, I/O, a closed
// handle) is rethrown, so a busy index is never reported as damaged.
const DAMAGE_CODES: ReadonlySet<string> = new Set(["SQLITE_NOTADB", "SQLITE_CORRUPT"]);

export function damageAsNull<T>(read: () => T): T | null {
  try {
    return read();
  } catch (e) {
    if (e instanceof Error && (e.message.startsWith("no such table:") || DAMAGE_CODES.has((e as Error & { code?: unknown }).code as string))) return null;
    throw e;
  }
}

// Every table a reader of the index relies on; a missing one makes the whole index unusable.
const TABLES = ["meta", "layers", "files", "symbols"] as const;

export type IndexRead<T> = { state: "missing" } | { state: "damaged"; detail: string } | { state: "ok"; value: T };

// Opens the index read-only, checks every table, runs `read` and closes it. "missing": no file, or another schema version.
export function readIndex<T>(file: string, read: (db: IndexDb) => T): IndexRead<T> {
  const opened = damageAsNull(() => ({ db: openIndexReadOnly(file) }));
  if (opened === null) return { state: "damaged", detail: "not a database" };
  const db = opened.db;
  if (db === null) return { state: "missing" };
  try {
    for (const t of TABLES) {
      if (damageAsNull(() => db.prepare(`SELECT 1 FROM ${t} LIMIT 1`).all()) === null) return { state: "damaged", detail: `no usable ${t} table` };
    }
    return { state: "ok", value: read(db) };
  } finally {
    db.close();
  }
}

// The one test of "is this index built" (its structure layer is ok), for doctor and publish alike: "unbuilt" when it
// isn't, and then `read` doesn't run.
export function readBuiltIndex<T>(file: string, read: (db: IndexDb) => T): IndexRead<T> | { state: "unbuilt" } {
  const r = readIndex(file, (db) => (layers(db).some((l) => l.layer === "structure" && l.status === "ok") ? { value: read(db) } : null));
  if (r.state !== "ok") return r;
  return r.value === null ? { state: "unbuilt" } : { state: "ok", value: r.value.value };
}

export const indexBuilt = (file: string): boolean => readBuiltIndex(file, () => true).state === "ok";
