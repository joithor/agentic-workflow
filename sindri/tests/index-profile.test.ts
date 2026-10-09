import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { LEDGER_SCHEMA_VERSION, ledgerFileVersion, openLedger, openLedgerReadOnly, openMemoryLedger } from "../src/ledger/db.js";
import { tempDir } from "./helpers.js";
import { isLoopbackUrl } from "../src/index/loopback.js";
import { DEFAULT_DENY_PATHS, denyPathsFor, ProfileSchema, RepoSchema } from "../src/profile/schema.js";

const base = { schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"] };

describe("index and shape profile keys", () => {
  it("defaults every index and shape key", () => {
    const p = ProfileSchema.parse(base);
    expect(p.index.embeddings).toEqual({ enabled: true, url: "http://127.0.0.1:11434", model: "nomic-embed-text" });
    expect(p.index.graph).toBe("graphify");
    expect(p.index.denyPaths).toEqual([]);
    expect(DEFAULT_DENY_PATHS).toEqual(expect.arrayContaining(["**/*.pem", "**/*.p12", "**/id_rsa*", "**/.npmrc", "**/secrets/**"]));
    expect(p.shape.thresholds).toEqual({
      nameSimilarity: 0.85, embedding: 0.9, embeddingAst: 0.6, nearCloneTokens: 60, nearCloneJaccard: 0.8, callOverlap: 0.5, complexityDelta: 10,
    });
    expect(p.shape.sizeBudget).toEqual({ XS: 80, S: 250, M: 600, L: 1200, XL: 2400 });
    expect(p.shape.exportAllowance).toEqual({ XS: 1, S: 3, M: 6, L: 10, XL: 20 });
    expect(p.shape).toMatchObject({ record: true, budgetMs: 2000, outcomeDays: 14, defaultSize: "S" });
    expect(RepoSchema.parse({ schemaVersion: 1, name: "r", path: "/r" }).index).toEqual({ denyPaths: [] });
  });

  // Like scrub.extraPatterns: configured globs add to the built-in secret globs, never replace them.
  it("adds the profile's and the repo's denyPaths to the built-in secret globs", () => {
    const p = ProfileSchema.parse({ ...base, index: { denyPaths: ["fixtures/phi/**"] } });
    expect(denyPathsFor(p.index, { denyPaths: ["gen/**"] })).toEqual([...DEFAULT_DENY_PATHS, "fixtures/phi/**", "gen/**"]);
  });

  it("refuses an embedding URL that is not loopback, or carries credentials (offline guarantee)", () => {
    for (const url of ["https://embeddings.example.com", "http://localhost:11434", "http://user:" + "pw@127.0.0.1:11434"]) {
      const r = ProfileSchema.safeParse({ ...base, index: { embeddings: { url } } });
      expect(r.success).toBe(false);
      if (!r.success) expect(r.error.issues[0].message).toContain("loopback");
    }
  });

  it("refuses a cloud embedding model, in any case", () => {
    for (const model of ["gpt-oss:120b-cloud", "Nomic-CLOUD"]) {
      const r = ProfileSchema.safeParse({ ...base, index: { embeddings: { model } } });
      expect(r.success).toBe(false);
      if (!r.success) expect(r.error.issues[0].message).toBe("cloud models send code off the machine");
    }
    expect(ProfileSchema.safeParse({ ...base, index: { embeddings: { model: "mxbai-embed-large" } } }).success).toBe(true);
  });

  it("recognizes loopback IP literals only", () => {
    for (const u of ["http://127.0.0.1:11434", "https://127.0.0.1/", "http://[::1]:11434"]) expect(isLoopbackUrl(u)).toBe(true);
    for (const u of ["http://localhost:11434/", "http://10.0.0.5:11434", "https://localhost.example.com", "http://127.0.0.1.nip.io", "not a url", "file:///tmp", "http://u:" + "p@127.0.0.1:1"]) {
      expect(isLoopbackUrl(u)).toBe(false);
    }
  });
});

describe("ledger migration v2", () => {
  it("adds shape_runs and shape_signals with the outcome columns", () => {
    const db = openMemoryLedger();
    expect(LEDGER_SCHEMA_VERSION).toBe(2);
    const cols = (t: string) => (db.prepare(`PRAGMA table_info(${t})`).all() as { name: string }[]).map((c) => c.name);
    expect(cols("shape_runs")).toEqual(expect.arrayContaining(["tree", "commit_sha", "index_age_ms", "providers", "parser", "deferred"]));
    expect(cols("shape_signals")).toEqual(expect.arrayContaining(["name", "ast_hash", "outcome", "labeled_at"]));
  });

  it("migrates a v1 ledger file in place and keeps a backup", () => {
    const file = path.join(tempDir(), "ledger.db");
    const v1 = openLedger(file);
    v1.pragma("user_version = 1");
    v1.exec("DROP TABLE shape_signals; DROP TABLE shape_runs;");
    v1.close();
    const db = openLedger(file);
    expect(db.pragma("user_version", { simple: true })).toBe(2);
    expect(fs.existsSync(`${file}.bak-v1`)).toBe(true);
    db.close();
  });

  it("keeps the rows of a v1 ledger that has items and events", () => {
    const file = path.join(tempDir(), "ledger.db");
    const v1 = openLedger(file);
    v1.pragma("user_version = 1");
    v1.exec("DROP TABLE shape_signals; DROP TABLE shape_runs;");
    v1.prepare(
      "INSERT INTO items (source, id, title, state, content_hash, first_seen, last_seen, epoch) VALUES ('s', 'i1', 'T', 'open', 'h', 't0', 't1', 0)",
    ).run();
    v1.prepare("INSERT INTO item_events (source, item_id, ts, kind, detail, epoch, tick_id) VALUES ('s', 'i1', 't1', 'seen', '{}', 0, 'tick')").run();
    v1.close();
    const db = openLedger(file);
    expect(db.prepare("SELECT source, id, title FROM items").all()).toEqual([{ source: "s", id: "i1", title: "T" }]);
    expect(db.prepare("SELECT item_id, kind FROM item_events").all()).toEqual([{ item_id: "i1", kind: "seen" }]);
    expect(db.prepare("SELECT COUNT(*) AS n FROM shape_runs").get()).toEqual({ n: 0 });
    expect(fs.existsSync(`${file}.bak-v1`)).toBe(true);
    db.close();
  });
});

describe("ledgerFileVersion", () => {
  it("is null for a missing file and the stored version otherwise", () => {
    const file = path.join(tempDir(), "ledger.db");
    expect(ledgerFileVersion(file)).toBeNull();
    openLedger(file).close();
    expect(ledgerFileVersion(file)).toBe(LEDGER_SCHEMA_VERSION);
  });
});

describe("openLedgerReadOnly (the hook never writes the ledger)", () => {
  it("is null for a missing file or another schema version, and refuses writes", () => {
    const file = path.join(tempDir(), "ledger.db");
    expect(openLedgerReadOnly(file)).toBeNull();
    openLedger(file).close();
    const ro = openLedgerReadOnly(file);
    if (ro === null) throw new Error("expected a read-only ledger");
    expect(ro.prepare("SELECT COUNT(*) AS n FROM shape_runs").get()).toEqual({ n: 0 });
    expect(() => ro.exec("CREATE TABLE sneaky (a)")).toThrow(/readonly/i);
    ro.close();
    const raw = new Database(file);
    raw.pragma("user_version = 1");
    raw.close();
    expect(openLedgerReadOnly(file)).toBeNull();
  });

  it("leaves the directory listing and the ledger bytes unchanged", () => {
    const file = path.join(tempDir(), "ledger.db");
    openLedger(file).close();
    const before = { dir: fs.readdirSync(path.dirname(file)).sort(), bytes: fs.readFileSync(file) };
    const ro = openLedgerReadOnly(file);
    if (ro === null) throw new Error("expected a read-only ledger");
    ro.prepare("SELECT COUNT(*) AS n FROM shape_runs").get();
    ro.close();
    expect(fs.readdirSync(path.dirname(file)).sort()).toEqual(before.dir);
    expect(fs.readFileSync(file).equals(before.bytes)).toBe(true);
  });
});
