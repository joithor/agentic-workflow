import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import YAML from "yaml";

import type { Deps } from "../src/deps.js";
import { runCli } from "../src/main.js";
import { sanitizeName } from "../src/profile/commands.js";
import { approveProfile, approvedProfile, snapshotDir } from "../src/profile/approve.js";
import { loadProfile } from "../src/profile/load.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { fakeGit, makeDeps, tempDir } from "./helpers.js";

function deps(over: Partial<Deps> = {}): Deps {
  const d = makeDeps(over);
  return { ...d, env: { ...d.env, USER: "Joi.T" } };
}

const profileDir = (d: Deps) => path.join(d.env.AW_STATE_DIR as string, "profile");

function ring0Repo(withPlans = true): string {
  const root = tempDir("sindri-repo-");
  if (withPlans) fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  return root;
}

function ring0Git(root: string, email: string | null) {
  return fakeGit({
    "rev-parse --show-toplevel": { ok: true, stdout: `${root}\n` },
    "config user.email": email === null ? { ok: false, stderr: "" } : { ok: true, stdout: `${email}\n` },
  });
}

describe("sanitizeName", () => {
  it("makes a valid claim namespace", () => {
    expect(sanitizeName("Joi.T")).toBe("joi-t");
    expect(sanitizeName("--x--")).toBe("x--");
    expect(sanitizeName("!!!")).toBe("me");
    expect(sanitizeName("a".repeat(50))).toHaveLength(39);
  });
});

describe("profile init", () => {
  it("scaffolds the generic profile with this user and host, then refuses to overwrite", async () => {
    const d = deps();
    const r = await runCli(["profile", "init"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("(mode: shadow)");
    const text = fs.readFileSync(path.join(profileDir(d), "profile.yaml"), "utf8");
    expect(text).toContain("user: joi-t");
    expect(text).toContain("active: test-host");
    expect((await runCli(["profile", "validate"], d)).stdout).toMatch(/^Profile valid\./);
    const again = await runCli(["profile", "init"], d);
    expect(again.exitCode).toBe(2);
    expect(again.stderr).toContain("SND-PROFILE-007");
    expect((await runCli(["profile", "init", "--force"], d)).exitCode).toBe(0);
  });

  it("writes private files and never through a symlink", async () => {
    const d = deps();
    const decoy = path.join(tempDir(), "decoy.txt");
    fs.writeFileSync(decoy, "original");
    fs.mkdirSync(profileDir(d), { recursive: true });
    fs.symlinkSync(decoy, path.join(profileDir(d), "profile.yaml"));
    const r = await runCli(["profile", "init"], d);
    expect(r.stderr).toContain("SND-PROFILE-007");
    expect((await runCli(["profile", "init", "--force"], d)).exitCode).toBe(0);
    expect(fs.readFileSync(decoy, "utf8")).toBe("original");
    expect(fs.statSync(path.join(profileDir(d), "profile.yaml")).mode & 0o777).toBe(0o600);
  });

  it("links $AW_STATE_DIR/profile to --dir, and says so when the link points elsewhere", async () => {
    const d = deps();
    const a = path.join(tempDir(), "a");
    const b = path.join(tempDir(), "b");
    const first = await runCli(["profile", "init", "--dir", a], d);
    expect(first.stdout).toContain(`Linked ${profileDir(d)} -> ${a}`);
    expect(fs.realpathSync(profileDir(d))).toBe(fs.realpathSync(a));
    const second = await runCli(["profile", "init", "--dir", b], d);
    expect(second.stdout).toContain("points elsewhere");
    fs.rmSync(a, { recursive: true });
    const dangling = await runCli(["profile", "init", "--dir", b, "--force"], d);
    expect(dangling.stdout).toContain("points elsewhere");
  });

  it("--ring0 builds a plan-file profile for the current repo (spec §13.3)", async () => {
    const root = ring0Repo();
    const d = deps({ cwd: root, git: ring0Git(root, "me@example.com") });
    const r = await runCli(["profile", "init", "--ring0", "--plans", "*-sindri-plan-*", "--json"], d);
    expect(r.exitCode).toBe(0);
    const name = sanitizeName(path.basename(root));
    const repoText = fs.readFileSync(path.join(profileDir(d), "repos", `${name}.yaml`), "utf8");
    expect(repoText).toContain(`path: ${root}`);
    const profileText = fs.readFileSync(path.join(profileDir(d), "profile.yaml"), "utf8");
    expect(profileText).toContain("type: plan-file");
    expect(profileText).toContain("- me@example.com");
    expect((YAML.parse(profileText) as { tracker: { include: string[] } }).tracker.include).toEqual(["*-sindri-plan-*"]);
    expect((await runCli(["profile", "validate"], d)).exitCode).toBe(0);
  });

  it("--ring0 without a git email trusts nobody, and refuses outside a plan repo or git", async () => {
    const root = ring0Repo();
    const d = deps({ cwd: root, git: ring0Git(root, null) });
    await runCli(["profile", "init", "--ring0"], d);
    expect(fs.readFileSync(path.join(profileDir(d), "profile.yaml"), "utf8")).toContain("trustedAuthors: []");
    const noPlans = ring0Repo(false);
    const r1 = await runCli(["profile", "init", "--ring0"], deps({ cwd: noPlans, git: ring0Git(noPlans, null) }));
    expect(r1.stderr).toContain("SND-PROFILE-008");
    const r2 = await runCli(["profile", "init", "--ring0"], deps({ git: fakeGit({}) }));
    expect(r2.stderr).toContain("SND-PROFILE-009");
  });
});

describe("profile validate / explain / migrate", () => {
  it("validate reports no profile, then issues as text and JSON", async () => {
    const d = deps();
    expect((await runCli(["profile", "validate"], d)).stderr).toContain("SND-PROFILE-002");
    await runCli(["profile", "init"], d);
    const file = path.join(profileDir(d), "profile.yaml");
    fs.appendFileSync(file, "\nbogus: 1\n");
    const text = await runCli(["profile", "validate"], d);
    expect(text.exitCode).toBe(2);
    expect(text.stderr).toContain("SND-PROFILE-001 profile has 1 issue(s)");
    expect(text.stderr).toContain("profile.yaml: Unrecognized key(s) in object: 'bogus' (fix: remove the key or fix its spelling)");
    const json = JSON.parse((await runCli(["profile", "validate", "--json"], d)).stdout);
    expect(json.ok).toBe(false);
    expect(json.error.details[0]).toMatch(/^profile\.yaml/);
    fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("bogus: 1", "").replace("mode: shadow", "mode: turbo"));
    expect((await runCli(["profile", "validate"], d)).stderr).toMatch(/profile\.yaml: mode: Invalid enum value/);
  });

  it("explain shows value and source, and rejects unknown keys and repos", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    expect((await runCli(["profile", "explain", "mode"], d)).stdout).toBe('mode = "shadow"  (from profile.yaml)\n');
    expect((await runCli(["profile", "explain", "nope"], d)).stderr).toContain("SND-PROFILE-003");
    expect((await runCli(["profile", "explain", "mode", "--repo", "zzz"], d)).stderr).toContain("SND-PROFILE-004");
    expect((await runCli(["profile", "explain"], d)).stderr).toContain("SND-CLI-002");
    const json = JSON.parse((await runCli(["profile", "explain", "selfMerge", "--repo", "example", "--json"], d)).stdout);
    expect(json).toEqual({ key: "selfMerge", value: "human", source: "profile.yaml" });
  });

  it("explain and migrate say when there is no profile, and migrate copes with no repos dir", async () => {
    const d = deps();
    expect((await runCli(["profile", "explain", "mode"], d)).stderr).toContain("SND-PROFILE-002");
    expect((await runCli(["profile", "migrate"], d)).stderr).toContain("SND-PROFILE-002");
    await runCli(["profile", "init"], d);
    fs.rmSync(path.join(profileDir(d), "repos"), { recursive: true });
    expect((await runCli(["profile", "migrate"], d)).stdout).toContain("Nothing to migrate.");
  });

  it("validate on an invalid profile blocks explain with SND-PROFILE-001", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    fs.appendFileSync(path.join(profileDir(d), "profile.yaml"), "\nbogus: 1\n");
    expect((await runCli(["profile", "explain", "mode"], d)).stderr).toContain("SND-PROFILE-001");
  });

  it("migrate: current, newer, wrong type and unreadable", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const file = path.join(profileDir(d), "profile.yaml");
    expect((await runCli(["profile", "migrate", "--dry-run"], d)).stdout).toBe("Profile is at schemaVersion 1 (current). Nothing to migrate.\n");
    const original = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, original.replace("schemaVersion: 1", "schemaVersion: 2"));
    expect((await runCli(["profile", "migrate"], d)).stderr).toContain("SND-PROFILE-005 profile.yaml has schemaVersion 2");
    fs.writeFileSync(file, original.replace("schemaVersion: 1", "schemaVersion: one"));
    expect((await runCli(["profile", "migrate"], d)).stderr).toContain("SND-PROFILE-001 profile.yaml: schemaVersion must be 1");
    fs.writeFileSync(file, "a: [broken\n");
    expect((await runCli(["profile", "migrate"], d)).stderr).toContain("SND-PROFILE-001 profile.yaml: not valid YAML");
  });
});

describe("profile approve (spec §8.7)", () => {
  it("shows the diff, refuses a short or stale hash, approves, then shows later changes", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const pending = await runCli(["profile", "approve"], d);
    expect(pending.exitCode).toBe(1);
    expect(pending.stdout).toContain("No approved profile yet");
    expect(pending.stdout).toContain("+ schemaVersion: 1");
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    expect((await runCli(["profile", "approve", hash.slice(0, 6)], d)).stderr).toContain("SND-CLI-002");
    expect((await runCli(["profile", "approve", "0".repeat(12)], d)).stderr).toContain("SND-PROFILE-006");
    expect((await runCli(["profile", "approve", hash.slice(0, 12)], d)).stderr).toContain("SND-PROFILE-010");
    const human = { ...d, isTTY: true, prompt: async () => "nope" };
    expect((await runCli(["profile", "approve", hash.slice(0, 12)], human)).stderr).toContain("SND-PROFILE-011");
    const ok = await runCli(["profile", "approve", hash.slice(0, 12)], { ...human, prompt: async () => hash.slice(0, 6) });
    expect(ok.stdout).toBe(`Approved profile ${hash.slice(0, 12)}. It takes effect on the next run.\n`);
    expect(fs.existsSync(path.join(snapshotDir(d, hash), "profile.yaml"))).toBe(true);
    expect(fs.statSync(path.join(snapshotDir(d, hash), "profile.yaml")).mode & 0o777).toBe(0o600);
    expect((await runCli(["profile", "approve"], d)).stdout).toBe(`Profile ${hash.slice(0, 12)} is approved.\n`);
    const file = path.join(profileDir(d), "profile.yaml");
    fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("selfMerge: human", "selfMerge: auto"));
    const changed = await runCli(["profile", "approve"], d);
    expect(changed.stdout).toContain("--- profile.yaml");
    expect(changed.stdout).toContain("- selfMerge: human");
    expect(changed.stdout).toContain("+ selfMerge: auto");
    expect(changed.stdout).not.toContain("repos/example.yaml");
  });

  it("rolls back by re-approving an earlier profile", async () => {
    let t = Date.parse("2026-10-08T12:00:00Z");
    const d = { ...deps(), now: () => new Date((t += 60_000)) };
    await runCli(["profile", "init"], d);
    const file = path.join(profileDir(d), "profile.yaml");
    const approve = async () => {
      const h = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
      await runCli(["profile", "approve", h], { ...d, isTTY: true, prompt: async () => h.slice(0, 6) });
      return h;
    };
    const a = await approve();
    const original = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, original.replace("selfMerge: human", "selfMerge: auto"));
    await approve();
    fs.writeFileSync(file, original);
    expect(await approve()).toBe(a);
    const db = openLedger(ledgerPath(path.join(d.env.AW_STATE_DIR as string, "sindri")));
    expect(approvedProfile(d, db)?.hash).toBe(a);
    db.close();
  });

  it("refuses to approve while another run holds the lock", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    const lock = path.join(d.env.AW_STATE_DIR as string, "sindri", "sindri.lock");
    fs.mkdirSync(lock, { recursive: true });
    fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 1, pidStartTime: "start-1", host: "test-host", bootId: "boot-1", startedAt: "t0", epoch: 1 }));
    const r = await runCli(["profile", "approve", hash.slice(0, 12)], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    expect(r.stderr).toContain("SND-LOCK-001 locked by test-host/1 since t0");
  });

  it("rejects unknown subcommands and flags", async () => {
    expect((await runCli(["profile"], deps())).stderr).toContain("SND-CLI-002 unknown profile subcommand: (none)");
    expect((await runCli(["profile", "frob"], deps())).stderr).toContain("SND-CLI-002");
    expect((await runCli(["profile", "validate", "--nope"], deps())).stderr).toContain("SND-CLI-002");
  });
});

describe("coverage of fallbacks and snapshot edge cases", () => {
  it("init falls back to the user name 'me' when USER is unset", async () => {
    const base = makeDeps();
    const env = { ...base.env };
    delete env.USER;
    const d = { ...base, env };
    await runCli(["profile", "init"], d);
    expect(fs.readFileSync(path.join(profileDir(d), "profile.yaml"), "utf8")).toContain("user: me");
  });

  it("approveProfile refuses when the snapshot hash differs from the validated one", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const loaded = loadProfile(profileDir(d));
    if (!loaded.ok) throw new Error("profile should load");
    const db = openLedger(ledgerPath(path.join(d.env.AW_STATE_DIR as string, "sindri")));
    try {
      expect(() => approveProfile(d, db, { ...loaded.value, hash: "0".repeat(64) })).toThrow(/SND-PROFILE-006|doesn't match/);
    } finally {
      db.close();
    }
  });

  it("approvedProfile is null with no approval and when the snapshot is gone", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const stateRoot = path.join(d.env.AW_STATE_DIR as string, "sindri");
    const db = openLedger(ledgerPath(stateRoot));
    try {
      expect(approvedProfile(d, db)).toBeNull();
      const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
      await runCli(["profile", "approve", hash.slice(0, 12)], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
      expect(approvedProfile(d, db)?.hash).toBe(hash);
      fs.rmSync(snapshotDir(d, hash), { recursive: true });
      expect(approvedProfile(d, db)).toBeNull();
    } finally {
      db.close();
    }
  });

  it("the diff treats a missing snapshot repos dir as empty", async () => {
    const d = deps();
    await runCli(["profile", "init"], d);
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], d)).stdout).hash as string;
    await runCli(["profile", "approve", hash.slice(0, 12)], { ...d, isTTY: true, prompt: async () => hash.slice(0, 6) });
    fs.rmSync(path.join(snapshotDir(d, hash), "repos"), { recursive: true });
    fs.appendFileSync(path.join(profileDir(d), "profile.yaml"), "# touch\n");
    const r = await runCli(["profile", "approve"], d);
    expect(r.stdout).toContain("--- repos/example.yaml");
  });
});
