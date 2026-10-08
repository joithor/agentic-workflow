import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import { LEDGER_SCHEMA_VERSION } from "../src/ledger/db.js";
import { runChecks } from "../src/doctor/doctor.js";
import { runCli } from "../src/main.js";
import { fakeSystem, makeDeps, tempDir } from "./helpers.js";

const byName = async (deps: Deps, node = "22.10.0") => Object.fromEntries((await runChecks(deps, node)).map((c) => [c.name, c]));

async function ring0Deps(): Promise<Deps> {
  const root = tempDir("sindri-doc-");
  fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  execFileSync("git", ["init", "-q"], { cwd: root });
  const d = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], d);
  return d;
}

describe("sindri doctor", () => {
  it("on a fresh machine: warns about the missing state dir and profile, nothing fails", async () => {
    const d = makeDeps();
    const c = await byName(d);
    expect(c.node.status).toBe("ok");
    expect(c["state-dir"]).toMatchObject({ status: "warn", detail: "not created yet" });
    expect(c.ledger).toMatchObject({ status: "ok", detail: "no ledger yet" });
    expect(c.lock).toMatchObject({ status: "ok", detail: "free" });
    expect(c.profile).toMatchObject({ status: "warn", fix: "sindri profile init --ring0 (or sindri profile init)" });
    expect(c["profile-approved"]).toBeUndefined();
    const r = await runCli(["doctor"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/^ok {3}node/m);
    expect(r.stdout).toMatch(/^warn state-dir/m);
  });

  it("is all ok after ring-0 init, approve and pre-commit install (spec §13.3 evidence)", async () => {
    const d = await ring0Deps();
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    await runCli(["scrub", "--install-pre-commit"], d);
    const r = await runCli(["doctor"], d);
    expect(r.stdout).not.toMatch(/^(warn|fail)/m);
    expect(r.exitCode).toBe(0);
    const c = await byName(d);
    expect(c.budget.detail).toBe("unset (not enforced before rollout step 3a)");
    expect(Object.keys(c).some((k) => k.startsWith("pre-commit:"))).toBe(true);
  });

  it("warns on an unapproved profile, a missing hook and another active host; a set budget is ok", async () => {
    const d = await ring0Deps();
    const file = path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    fs.appendFileSync(file, "budget:\n  perItem: 1000\n  perDay: 5000\n");
    const c = await byName({ ...d, system: fakeSystem({ hostname: () => "other" }) });
    expect(c["profile-approved"].status).toBe("warn");
    expect(c["active-host"]).toMatchObject({ status: "warn", detail: "this host is other; hosts.active is test-host" });
    expect(c.budget).toMatchObject({ status: "ok", detail: "perItem 1000, perDay 5000 tokens" });
    const hook = Object.entries(c).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook?.status).toBe("warn");
  });

  it("warns when the installed hook points at a CLI that is gone", async () => {
    const d = await ring0Deps();
    await runCli(["scrub", "--install-pre-commit"], { ...d, env: { ...d.env, SINDRI_BIN: "/nonexistent/sindri" } });
    const hook = Object.entries(await byName(d)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook).toMatchObject({ status: "warn" });
    expect(hook?.detail).toContain("/nonexistent/sindri, which is missing");
  });

  it("fails on an old node, a network state dir, an invalid profile and a newer ledger", async () => {
    const d = makeDeps({ system: fakeSystem({ isLocalDisk: () => false, bootId: () => null }) });
    fs.mkdirSync(stateDir(d), { recursive: true });
    const raw = new Database(path.join(stateDir(d), "ledger.db"));
    raw.pragma("user_version = 99");
    raw.close();
    fs.mkdirSync(path.join(d.env.AW_STATE_DIR as string, "profile"), { recursive: true });
    fs.writeFileSync(path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "bogus: 1\n");
    const c = await byName(d, "18.19.0");
    expect(c.node.status).toBe("fail");
    expect(c["state-dir"]).toMatchObject({ status: "fail", detail: "on a network filesystem" });
    expect(c["boot-id"]).toMatchObject({ status: "ok", detail: "unreadable; stale-lock checks use pids only" });
    expect(c.ledger.status).toBe("fail");
    expect(c.ledger.detail).toContain("SND-LEDGER-001");
    expect(c.profile.status).toBe("fail");
    expect((await runCli(["doctor", "--json"], d)).exitCode).toBe(2);
  });

  it("warns on loose permissions, an unknown disk type, a stale lock and leftovers", async () => {
    const d = makeDeps({ system: fakeSystem({ isLocalDisk: () => null, pidAlive: () => false }) });
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o755);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), JSON.stringify({ pid: 9, pidStartTime: null, host: "test-host", bootId: "boot-1", startedAt: "t", epoch: 1 }));
    fs.mkdirSync(path.join(dir, "sindri.lock.stale-x"));
    const c = await byName(d);
    expect(c["state-dir"]).toMatchObject({ status: "warn", detail: "mode 755 (want 700); can't tell if it is on local disk" });
    expect(c.lock.status).toBe("warn");
    expect(c.lock.detail).toContain("stale lock from test-host/9");
    expect(c.lock.detail).toContain("leftovers: sindri.lock.stale-x");
  });

  it("reports a held lock, and a stale lock without leftovers, as ok", async () => {
    const d = makeDeps();
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o700);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), JSON.stringify({ pid: 9, pidStartTime: "start-9", host: "test-host", bootId: "boot-1", startedAt: "t", epoch: 1 }));
    expect((await byName(d)).lock).toMatchObject({ status: "ok", detail: "held by test-host/9 since t" });
    const stale = await byName({ ...d, system: fakeSystem({ pidAlive: () => false }) });
    expect(stale.lock).toMatchObject({ status: "ok", detail: "stale lock from test-host/9; the next run takes it over" });
  });
});

describe("sindri doctor fix hints clear their warning", () => {
  it("state-dir: the suggested `sindri profile init` creates the state dir (mode 700)", async () => {
    const d = makeDeps();
    const before = await byName(d);
    expect(before["state-dir"]).toMatchObject({ status: "warn", detail: "not created yet", fix: "sindri profile init --ring0 (or sindri profile init)" });
    await runCli(["profile", "init"], d);
    expect((await byName(d))["state-dir"]).toMatchObject({ status: "ok", detail: stateDir(d) });
    expect(fs.statSync(stateDir(d)).mode & 0o777).toBe(0o700);
  });
});

describe("sindri doctor coverage paths", () => {
  it("reports an executable hook binary as ok", async () => {
    const d = await ring0Deps();
    await runCli(["scrub", "--install-pre-commit"], { ...d, env: { ...d.env, SINDRI_BIN: process.execPath } });
    const hook = Object.entries(await byName(d)).find(([k]) => k.startsWith("pre-commit:"))?.[1];
    expect(hook?.status).toBe("ok");
    expect(hook?.detail).toContain(process.execPath);
  });

  it("fails on an unreadable ledger with no error code, and does not touch it", async () => {
    const d = await ring0Deps();
    const file = path.join(stateDir(d), "ledger.db");
    fs.mkdirSync(stateDir(d), { recursive: true });
    fs.writeFileSync(file, "not a db");
    const c = await byName(d);
    expect(c.ledger.status).toBe("fail");
    expect(c.ledger.detail).not.toMatch(/^SND-/);
    expect(c["profile-approved"].status).toBe("warn");
    expect(fs.readFileSync(file, "utf8")).toBe("not a db");
  });

  it("prints unset for the missing half of a partial budget", async () => {
    const d = await ring0Deps();
    const file = path.join(d.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    const base = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, `${base}budget:\n  perItem: 1000\n`);
    expect((await byName(d)).budget.detail).toBe("perItem 1000, perDay unset tokens");
    fs.writeFileSync(file, `${base}budget:\n  perDay: 5000\n`);
    expect((await byName(d)).budget.detail).toBe("perItem unset, perDay 5000 tokens");
  });

  it("reports an unknown flag as SND-CLI-002", async () => {
    const r = await runCli(["doctor", "--nope"], makeDeps());
    expect(r.exitCode).toBe(2);
    expect(r.stderr + r.stdout).toContain("SND-CLI-002");
  });

  it("flags node 20.10 as too old and 20.11 as fine", async () => {
    const d = makeDeps();
    expect((await byName(d, "20.10.0")).node.status).toBe("fail");
    expect((await byName(d, "20.11.0")).node.status).toBe("ok");
  });
});

describe("sindri doctor lock and read-only guarantees", () => {
  it("names an unreadable lock owner without a since clause", async () => {
    const d = makeDeps();
    const dir = stateDir(d);
    fs.mkdirSync(path.join(dir, "sindri.lock"), { recursive: true });
    fs.chmodSync(dir, 0o700);
    fs.writeFileSync(path.join(dir, "sindri.lock", "owner.json"), "{not json");
    const lock = (await byName(d)).lock;
    expect(lock.status).toBe("ok");
    expect(lock.detail).toMatch(/unreadable owner/);
    expect(lock.detail).not.toContain("since");
    expect(lock.detail).not.toContain("undefined");
  });

  it("does not migrate, back up, chmod or leave files beside a valid older WAL ledger (m19)", async () => {
    const d = await ring0Deps();
    const dir = stateDir(d);
    fs.mkdirSync(dir, { recursive: true });
    fs.chmodSync(dir, 0o700);
    const file = path.join(dir, "ledger.db");
    // Like a real ledger: WAL mode, last writer closed cleanly (no -wal or -shm left).
    const raw = new Database(file);
    raw.pragma("journal_mode = WAL");
    raw.exec("CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
    raw.exec("CREATE TABLE profile_approvals (hash TEXT PRIMARY KEY, approved_at TEXT NOT NULL, approved_by TEXT NOT NULL)");
    raw.pragma("user_version = 1");
    raw.close();
    fs.chmodSync(file, 0o644);
    const before = fs.readdirSync(dir).sort();
    expect(before.filter((n) => n.startsWith("ledger.db"))).toEqual(["ledger.db"]);
    const bytes = fs.readFileSync(file);
    const r = await runCli(["doctor"], d);
    expect(r.stdout).toMatch(/^ok {3}ledger/m);
    const check = Object.fromEntries((await runChecks(d, "22.10.0")).map((c) => [c.name, c]));
    expect(check.ledger.detail).toBe(`schema v1 of ${LEDGER_SCHEMA_VERSION}`);
    expect(check["profile-approved"].detail).toContain("has never been approved");
    expect(fs.statSync(file).mode & 0o777).toBe(0o644);
    expect(fs.readdirSync(dir).sort()).toEqual(before);
    expect(fs.readFileSync(file).equals(bytes)).toBe(true);
  });
});
