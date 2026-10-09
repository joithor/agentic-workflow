import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { LEDGER_SCHEMA_VERSION, openLedger, openMemoryLedger } from "../src/ledger/db.js";
import { ProfileSchema } from "../src/profile/schema.js";
import { tempDir } from "./helpers.js";

const base = { schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"] };

describe("ledger v4", () => {
  it("adds the evolve tables and keeps hook samples unique by ref", () => {
    expect(LEDGER_SCHEMA_VERSION).toBeGreaterThanOrEqual(4);
    const db = openMemoryLedger();
    const names = (db.prepare("SELECT name FROM sqlite_master WHERE type = 'table'").all() as { name: string }[]).map((r) => r.name);
    expect(names).toEqual(expect.arrayContaining(["adoptions", "artifacts", "comparisons", "evolve_audit", "hook_samples", "proposals", "suite_runs"]));
    const insert = "INSERT INTO hook_samples (hook, ref, ts, warranted, reason, sampled_at, epoch) VALUES ('h', 'r', 't', ?, 'x', 't', 1)";
    db.prepare(insert).run(null);
    expect(() => db.prepare(insert).run(1)).toThrow(/UNIQUE/);
  });

  it("upgrades a v3 ledger with rows to v4, keeps the rows and writes .bak-v3", () => {
    const file = path.join(tempDir(), "ledger.db");
    const v3 = openLedger(file);
    v3.exec("DROP TABLE artifacts; DROP TABLE suite_runs; DROP TABLE proposals; DROP TABLE comparisons; DROP TABLE hook_samples; DROP TABLE adoptions; DROP TABLE evolve_audit;");
    v3.pragma("user_version = 3");
    v3.prepare(
      "INSERT INTO items (source, id, title, state, content_hash, first_seen, last_seen, epoch) VALUES ('s', 'i1', 'T', 'open', 'h', 't0', 't1', 0)",
    ).run();
    v3.close();
    const db = openLedger(file);
    expect(db.pragma("user_version", { simple: true })).toBe(LEDGER_SCHEMA_VERSION);
    expect(db.prepare("SELECT id FROM items").pluck().all()).toEqual(["i1"]);
    expect(db.prepare("SELECT COUNT(*) AS n FROM artifacts").get()).toEqual({ n: 0 });
    expect(fs.existsSync(`${file}.bak-v3`)).toBe(true);
    db.close();
  });
});

describe("evolve and privacy profile keys", () => {
  it("defaults the budgets, the cap and the deny list", () => {
    const p = ProfileSchema.parse(base);
    expect(p.evolve).toEqual({ maxOpenProposals: 10, maxTokensPerJob: 600000, maxTokensPerCompare: 3000000, maxCorrectTurns: 400, prAuthors: [] });
    expect(p.privacy).toEqual({ denyTerms: [] });
  });

  it("accepts deny terms and PR authors, and refuses unknown or malformed keys", () => {
    const ok = ProfileSchema.parse({ ...base, privacy: { denyTerms: ["Acme Care"] }, evolve: { maxOpenProposals: 3, prAuthors: ["joi-t"] } });
    expect(ok.privacy.denyTerms).toEqual(["Acme Care"]);
    expect(ok.evolve.prAuthors).toEqual(["joi-t"]);
    expect(ProfileSchema.safeParse({ ...base, privacy: { denyTerms: ["a"] } }).success).toBe(false);
    expect(ProfileSchema.safeParse({ ...base, privacy: { other: 1 } }).success).toBe(false);
    expect(ProfileSchema.safeParse({ ...base, evolve: { maxOpenProposals: 0 } }).success).toBe(false);
    expect(ProfileSchema.safeParse({ ...base, evolve: { prAuthors: ["not a login!"] } }).success).toBe(false);
    expect(ProfileSchema.parse({ ...base, evolve: { maxCorrectTurns: 2000 } }).evolve.maxCorrectTurns).toBe(2000);
    for (const bad of [0, 2001, 1.5]) expect(ProfileSchema.safeParse({ ...base, evolve: { maxCorrectTurns: bad } }).success, String(bad)).toBe(false);
  });
});
