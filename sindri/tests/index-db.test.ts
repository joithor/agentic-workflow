import Database from "better-sqlite3";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { INDEX_SCHEMA_VERSION, indexPath, meta, openIndex, openIndexReadOnly } from "../src/index/db.js";
import { makeDeps, tempDir } from "./helpers.js";

describe("index db", () => {
  it("lives under the sindri state dir, one file per repo", () => {
    const d = makeDeps({ env: { AW_STATE_DIR: "/s" } });
    expect(indexPath(d, "toolkit")).toBe("/s/sindri/index/toolkit.db");
  });

  it("creates the schema, and recreates a file from another indexer version", () => {
    const file = path.join(tempDir(), "x.db");
    const db = openIndex(file);
    expect(db.pragma("user_version", { simple: true })).toBe(INDEX_SCHEMA_VERSION);
    db.prepare("INSERT INTO files (path, hash, size) VALUES ('a.ts', 'h', 1)").run();
    expect(meta(db)).toEqual({ commit: "", builtAt: null });
    db.close();
    const raw = new Database(file);
    raw.pragma("user_version = 99");
    raw.close();
    const again = openIndex(file);
    expect(again.prepare("SELECT COUNT(*) AS n FROM files").get()).toEqual({ n: 0 });
    again.close();
  });

  it("reads only an index of the version it knows", () => {
    const dir = tempDir();
    expect(openIndexReadOnly(path.join(dir, "missing.db"))).toBeNull();
    const good = path.join(dir, "good.db");
    openIndex(good).close();
    const ro = openIndexReadOnly(good);
    expect(ro).not.toBeNull();
    ro?.close();
    const old = path.join(dir, "old.db");
    const raw = new Database(old);
    raw.pragma("user_version = 99");
    raw.close();
    expect(openIndexReadOnly(old)).toBeNull();
  });
});
