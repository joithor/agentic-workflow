import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import { runChecks } from "../src/doctor/doctor.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { listItems } from "../src/ledger/items.js";
import { runCli } from "../src/main.js";
import { approvalState, snapshotDir } from "../src/profile/approve.js";
import { requireProfile } from "../src/profile/commands.js";
import { makeDeps, tempDir } from "./helpers.js";

// Spec §8.7 + plan amendment 8: "approved" means the live profile's hash is the
// LATEST approval and that approval's snapshot loads. approve, doctor and observe
// must all agree on it.

const PLAN = ["# Plan A", "", "### Task 1: Small", "", "- [ ] **Step 1: x**", ""].join("\n");

async function setup(): Promise<{ deps: Deps; file: string }> {
  const root = tempDir("sindri-appr-");
  fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  fs.writeFileSync(path.join(root, "docs/superpowers/plans/2026-01-01-plan-a.md"), PLAN);
  execFileSync("git", ["-c", "user.name=T", "-c", "user.email=me@example.com", "init", "-q"], { cwd: root });
  // A fixed clock: approval order must not depend on wall-clock time.
  const deps = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], deps);
  return { deps, file: path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml") };
}

async function approveLive(deps: Deps): Promise<string> {
  const hash = JSON.parse((await runCli(["profile", "approve", "--json"], deps)).stdout).hash as string;
  const r = await runCli(["profile", "approve", hash], { ...deps, isTTY: true, prompt: async () => hash.slice(0, 6) });
  if (r.exitCode !== 0) throw new Error(r.stderr);
  return hash;
}

async function verdicts(deps: Deps) {
  const status = await runCli(["profile", "approve"], deps);
  const statusJson = JSON.parse((await runCli(["profile", "approve", "--json"], deps)).stdout);
  const doctor = Object.fromEntries((await runChecks(deps, "22.10.0")).map((c) => [c.name, c]))["profile-approved"];
  const observe = await runCli(["observe", "--no-record"], deps);
  return { status, statusJson, doctor, observe };
}

function state(deps: Deps) {
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    return approvalState(deps, db, requireProfile(deps).hash);
  } finally {
    db.close();
  }
}

describe("approval state (spec §8.7)", () => {
  it("never approved: all three commands say so", async () => {
    const { deps } = await setup();
    expect(state(deps)).toEqual({ kind: "never-approved" });
    const v = await verdicts(deps);
    expect(v.status.exitCode).toBe(1);
    expect(v.status.stdout).toContain("has never been approved");
    expect(v.statusJson).toMatchObject({ approved: false, state: "never-approved" });
    expect(v.doctor).toMatchObject({ status: "warn", fix: "sindri profile approve" });
    expect(v.doctor.detail).toContain("has never been approved");
    expect(v.observe.exitCode).toBe(1);
    expect(v.observe.stdout).toContain("has never been approved");
  });

  it("changed since approval: all three say the live profile differs from the approved one", async () => {
    const { deps, file } = await setup();
    const approved = await approveLive(deps);
    fs.appendFileSync(file, "# edited\n");
    const s = state(deps);
    expect(s.kind).toBe("changed-since-approval");
    if (s.kind === "changed-since-approval") expect(s.approved.hash).toBe(approved);
    const v = await verdicts(deps);
    const short = approved.slice(0, 12);
    expect(v.status.exitCode).toBe(1);
    expect(v.status.stdout).toContain(`differs from the approved profile ${short}`);
    expect(v.status.stdout).toContain("+ # edited");
    expect(v.statusJson).toMatchObject({ approved: false, state: "changed-since-approval", approvedHash: approved });
    expect(v.doctor).toMatchObject({ status: "warn", fix: "sindri profile approve" });
    expect(v.doctor.detail).toContain(`differs from the approved profile ${short}`);
    expect(v.observe.exitCode).toBe(1);
    expect(v.observe.stdout).toContain(`Using approved profile ${short}`);
  });

  it("doctor's profile-content checks read the approved snapshot, and say so, after an unapproved edit", async () => {
    const { deps, file } = await setup();
    const before = Object.fromEntries((await runChecks(deps, "22.10.0")).map((c) => [c.name, c]));
    expect(before["profile-in-use"].detail).toBe("live profile (nothing approved yet); the checks below read it");
    const approved = await approveLive(deps);
    fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace(/active: test-host/, "active: elsewhere") + "budget:\n  perItem: 7\n");
    const c = Object.fromEntries((await runChecks(deps, "22.10.0")).map((c) => [c.name, c]));
    expect(c["profile-approved"].status).toBe("warn");
    expect(c["profile-in-use"].detail).toBe(`approved profile ${approved.slice(0, 12)}; the checks below read it`);
    expect(c["active-host"]).toMatchObject({ status: "ok", detail: "test-host" });
    expect(c.budget.detail).toBe("unset (not enforced before rollout step 3a)");
  });

  it("a rollback re-approves an earlier hash, and all three commands then agree", async () => {
    const { deps, file } = await setup();
    const original = fs.readFileSync(file, "utf8");
    const h1 = await approveLive(deps);
    fs.appendFileSync(file, "# second\n");
    const h2 = await approveLive(deps);
    fs.writeFileSync(file, original);
    // H1 was approved once, but H2 is the latest approval, so H1 is not what runs use.
    const before = await verdicts(deps);
    expect(before.status.exitCode).toBe(1);
    expect(before.status.stdout).toContain(`differs from the approved profile ${h2.slice(0, 12)}`);
    expect(before.status.stdout).toContain("- # second");
    expect(before.doctor.status).toBe("warn");
    expect(before.observe.stdout).toContain(`Using approved profile ${h2.slice(0, 12)}`);
    // Re-approving H1 makes it the latest approval, even with the same clock reading.
    expect(await approveLive(deps)).toBe(h1);
    expect(state(deps)).toMatchObject({ kind: "approved", approved: { hash: h1 } });
    const after = await verdicts(deps);
    expect(after.status).toMatchObject({ exitCode: 0, stdout: `Profile ${h1.slice(0, 12)} is approved.\n` });
    expect(after.statusJson).toMatchObject({ approved: true, state: "approved" });
    expect(after.doctor).toMatchObject({ status: "ok", detail: h1.slice(0, 12) });
    expect(after.observe.exitCode).toBe(0);
    expect(after.observe.stdout).not.toContain("Using approved profile");
  });

  it("a missing snapshot: all three say re-approve, and re-approving restores it", async () => {
    const { deps } = await setup();
    const hash = await approveLive(deps);
    fs.rmSync(snapshotDir(deps, hash), { recursive: true });
    expect(state(deps)).toEqual({ kind: "snapshot-missing", hash });
    const v = await verdicts(deps);
    const short = hash.slice(0, 12);
    expect(v.status.exitCode).toBe(1);
    expect(v.status.stdout).toContain(`snapshot of approved profile ${short} is missing or invalid`);
    expect(v.status.stdout).toContain(`To approve: sindri profile approve ${short}`);
    expect(v.statusJson).toMatchObject({ approved: false, state: "snapshot-missing", approvedHash: hash });
    expect(v.doctor).toMatchObject({ status: "warn", fix: "sindri profile approve" });
    expect(v.doctor.detail).toContain(`snapshot of approved profile ${short} is missing or invalid; re-approve`);
    expect(v.observe.exitCode).toBe(1);
    expect(v.observe.stdout).toContain(`snapshot of approved profile ${short} is missing or invalid`);
    expect(v.observe.stdout).toContain("re-approve");
    // observe records nothing in that state.
    expect(await runCli(["observe"], deps)).toMatchObject({ exitCode: 1 });
    const db = openLedger(ledgerPath(stateDir(deps)));
    expect(listItems(db)).toEqual([]);
    db.close();
    await approveLive(deps);
    expect(state(deps)).toMatchObject({ kind: "approved", approved: { hash } });
  });

  it("an invalid snapshot counts as missing", async () => {
    const { deps } = await setup();
    const hash = await approveLive(deps);
    fs.appendFileSync(path.join(snapshotDir(deps, hash), "profile.yaml"), "# tampered\n");
    expect(state(deps)).toEqual({ kind: "snapshot-missing", hash });
  });
});
