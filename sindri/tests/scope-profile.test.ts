import { describe, expect, it } from "vitest";

import { openMemoryLedger, LEDGER_SCHEMA_VERSION } from "../src/ledger/db.js";
import { LINEAR_API_URL, ProfileSchema } from "../src/profile/schema.js";

const base = { schemaVersion: 1, user: "me", hosts: { active: "h" }, tracker: { type: "plan-file", repo: "r" }, repos: ["r"] };

describe("scope profile keys", () => {
  it("defaults sources (transcripts off), models and scope budgets", () => {
    const p = ProfileSchema.parse(base);
    expect(p.models).toEqual({ scoping: "sonnet", challenger: "opus", adjudicator: "opus", effort: "medium", allowBaseUrl: false });
    expect(p.scope).toEqual({ maxRounds: 3, maxTokensPerRun: 600000, maxTokensPerBacktest: 1500000, maxRecords: 40, maxPackChars: 120000 });
    expect(p.sources.transcripts).toEqual({ enabled: false, dir: "~/.claude/projects" });
    expect(p.sources.linear).toBeUndefined();
  });

  it("accepts a Linear source with a secret pointer, and refuses a raw token", () => {
    const ok = ProfileSchema.parse({ ...base, sources: { linear: { token: "keychain:linear/me" } } }).sources.linear;
    expect(ok).toEqual({ token: "keychain:linear/me", apiUrl: LINEAR_API_URL, allowCustomApiUrl: false });
    expect(ProfileSchema.safeParse({ ...base, sources: { linear: { token: "lin_api_raw" } } }).success).toBe(false);
  });

  it("pins the Linear API URL: https, the Linear host, unless a custom host is allowed", () => {
    const linear = (extra: object) => ProfileSchema.safeParse({ ...base, sources: { linear: { token: "env:T", ...extra } } });
    const plain = linear({ apiUrl: "http://api.linear.app/graphql" });
    expect(plain.success).toBe(false);
    if (!plain.success) expect(plain.error.issues[0].message).toContain("must be https://api.linear.app/graphql");
    expect(linear({ apiUrl: "https://proxy.example.com/graphql" }).success).toBe(false);
    expect(linear({ apiUrl: "https://proxy.example.com/graphql", allowCustomApiUrl: true }).success).toBe(true);
    expect(linear({ apiUrl: "http://proxy.example.com/graphql", allowCustomApiUrl: true }).success).toBe(false);
  });

  it("requires the challenger and adjudicator to differ from the drafter (spec §6.1)", () => {
    const r = ProfileSchema.safeParse({ ...base, models: { scoping: "opus", challenger: "opus" } });
    expect(r.success).toBe(false);
    if (!r.success) expect(r.error.issues[0].message).toContain("must differ from models.scoping");
  });

  it("validates model names and the effort level", () => {
    expect(ProfileSchema.safeParse({ ...base, models: { scoping: "--dangerously" } }).success).toBe(false);
    expect(ProfileSchema.safeParse({ ...base, models: { scoping: "sonnet[1m]" } }).success).toBe(true);
    expect(ProfileSchema.safeParse({ ...base, models: { effort: "max" } }).success).toBe(true);
    expect(ProfileSchema.safeParse({ ...base, models: { effort: "extreme" } }).success).toBe(false);
  });

  it("adds scope_runs and model_calls in ledger v3", () => {
    expect(LEDGER_SCHEMA_VERSION).toBeGreaterThanOrEqual(3); // later plans append migrations
    const db = openMemoryLedger();
    const names = (db.prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name IN ('scope_runs', 'model_calls') ORDER BY name").all() as { name: string }[]).map((r) => r.name);
    expect(names).toEqual(["model_calls", "scope_runs"]);
    const cols = (db.prepare("PRAGMA table_info(scope_runs)").all() as { name: string }[]).map((c) => c.name);
    expect(cols).toEqual(expect.arrayContaining(["recall", "precision", "baseline_recall", "baseline_precision", "leaky", "out_path"]));
  });

  it("model_calls audits any step: no foreign key to scope_runs, a step column defaulting to scope", () => {
    const db = openMemoryLedger();
    expect(db.pragma("foreign_keys", { simple: true })).toBe(1);
    expect(db.prepare("PRAGMA foreign_key_list(model_calls)").all()).toEqual([]);
    const step = (db.prepare("PRAGMA table_info(model_calls)").all() as { name: string; notnull: number; dflt_value: string | null }[]).find((c) => c.name === "step");
    expect(step).toMatchObject({ notnull: 1, dflt_value: "'scope'" });
    db.prepare("INSERT INTO model_calls (run_id, step, seq, role, model, input_tokens, output_tokens) VALUES ('no-scope-run', 'triage', 1, 'r', 'm', 1, 1)").run();
    db.prepare("INSERT INTO model_calls (run_id, seq, role, model, input_tokens, output_tokens) VALUES ('other', 1, 'r', 'm', 1, 1)").run();
    expect(db.prepare("SELECT run_id, step FROM model_calls ORDER BY run_id").all()).toEqual([{ run_id: "no-scope-run", step: "triage" }, { run_id: "other", step: "scope" }]);
  });
});
