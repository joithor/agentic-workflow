import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { openIndex } from "../src/index/db.js";
import { damageAsNull, indexBuilt, readBuiltIndex, readIndex } from "../src/index/health.js";
import { seedIndex } from "./index-fixtures.js";
import { tempDir } from "./helpers.js";

const fresh = (): string => path.join(tempDir("ix-"), "i.db");

describe("damageAsNull", () => {
  it("returns null for a missing table and for a not-a-database or corrupt code; rethrows anything else (M4)", () => {
    const db = openIndex(fresh());
    expect(damageAsNull(() => db.prepare("SELECT 1 FROM layers").all())).toEqual([]);
    db.exec("DROP TABLE layers");
    expect(damageAsNull(() => db.prepare("SELECT 1 FROM layers").all())).toBeNull();
    db.exec("CREATE VIEW layers AS SELECT json('{') AS layer");
    expect(() => damageAsNull(() => db.prepare("SELECT * FROM layers").all())).toThrow(/malformed JSON/);
    db.close();
    expect(() => damageAsNull(() => db.prepare("SELECT 1").all())).toThrow(/not open/);
    for (const code of ["SQLITE_NOTADB", "SQLITE_CORRUPT"]) expect(damageAsNull(() => { throw Object.assign(new Error("damaged"), { code }); })).toBeNull();
    expect(() => damageAsNull(() => { throw Object.assign(new Error("database is locked"), { code: "SQLITE_BUSY" }); })).toThrow(/locked/);
    expect(() => damageAsNull(() => { throw "not an Error"; })).toThrow();
  });
});

describe("readIndex", () => {
  it("says missing for no file and for another schema version", () => {
    expect(readIndex(fresh(), () => 1)).toEqual({ state: "missing" });
    const old = fresh();
    const db = new Database(old);
    db.pragma("user_version = 99");
    db.close();
    expect(readIndex(old, () => 1)).toEqual({ state: "missing" });
  });

  it("says damaged for a file that isn't a database and for each missing table, without running the read", () => {
    const file = fresh();
    fs.writeFileSync(file, "plain text where a SQLite header should be, long enough to fill a page".repeat(5));
    expect(readIndex(file, () => { throw new Error("must not read"); })).toEqual({ state: "damaged", detail: "not a database" });
    for (const table of ["meta", "layers", "files", "symbols"]) {
      const f = fresh();
      const db = seedIndex(openIndex(f), { files: ["a.ts"], symbols: ["x"] });
      db.exec(`DROP TABLE ${table}`);
      db.close();
      expect(readIndex(f, () => { throw new Error("must not read"); })).toEqual({ state: "damaged", detail: `no usable ${table} table` });
    }
  });

  it("runs the read on a usable index and closes it", () => {
    const f = fresh();
    seedIndex(openIndex(f), { files: ["a.ts"], symbols: ["x"] }).close();
    let held: Database.Database | null = null;
    expect(readIndex(f, (db) => { held = db; return db.prepare("SELECT COUNT(*) AS c FROM symbols").get(); })).toEqual({ state: "ok", value: { c: 1 } });
    expect(held!.open).toBe(false);
  });
});

describe("readBuiltIndex and indexBuilt", () => {
  it("is unbuilt while the structure layer isn't ok, ok once it is, and passes damage through", () => {
    const f = fresh();
    seedIndex(openIndex(f), { files: ["a.ts"], symbols: ["x"], structure: "pending" }).close();
    expect(readBuiltIndex(f, () => { throw new Error("must not read"); })).toEqual({ state: "unbuilt" });
    expect(indexBuilt(f)).toBe(false);
    const ok = fresh();
    seedIndex(openIndex(ok), { files: ["a.ts"], symbols: ["x"] }).close();
    expect(readBuiltIndex(ok, () => "read")).toEqual({ state: "ok", value: "read" });
    expect(indexBuilt(ok)).toBe(true);
    expect(readBuiltIndex(fresh(), () => 1)).toEqual({ state: "missing" });
    expect(indexBuilt(fresh())).toBe(false);
  });
});
