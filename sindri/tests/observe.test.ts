import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import { bumpEpoch, currentEpoch, ledgerPath, openLedger } from "../src/ledger/db.js";
import { listEvents, listItems } from "../src/ledger/items.js";
import { runCli } from "../src/main.js";
import { parseSince } from "../src/observe/observe.js";
import { fakeSystem, makeDeps, tempDir } from "./helpers.js";

const PLAN = [
  "# Plan A", "", "### Task 1: Small", "", "**Files:**", "- Create: `a.ts`", "", "- [ ] **Step 1: x**", "", "```ts", "x", "```", "",
  "### Task 2: Later", "", "- [ ] **Step 1: y**", "",
].join("\n");

const PLAN_FILE = "docs/superpowers/plans/2026-01-01-plan-a.md";

function planRepo(text = PLAN): string {
  const root = tempDir("sindri-obs-");
  fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  fs.writeFileSync(path.join(root, PLAN_FILE), text);
  const g = (...a: string[]) => execFileSync("git", ["-c", "user.name=T", "-c", "user.email=me@example.com", ...a], { cwd: root, stdio: "ignore" });
  g("init", "-q");
  g("add", ".");
  g("commit", "-qm", "plan");
  return root;
}

async function ring0(root: string): Promise<Deps> {
  const deps = makeDeps({ cwd: root });
  await runCli(["profile", "init", "--ring0"], deps);
  const file = path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml");
  fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace(/trustedAuthors:[\s\S]*$/, "trustedAuthors:\n  - me@example.com\n"));
  return deps;
}

// What the ledger holds: items, events and the epoch. A read-only path leaves it unchanged.
function ledgerState(deps: Deps): { items: unknown[]; events: unknown[]; epoch: number } | null {
  if (!fs.existsSync(ledgerPath(stateDir(deps)))) return null;
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    return { items: listItems(db), events: listEvents(db, {}), epoch: currentEpoch(db) };
  } finally {
    db.close();
  }
}

const repoFile = (deps: Deps): string => {
  const dir = path.join(deps.env.AW_STATE_DIR as string, "profile", "repos");
  return path.join(dir, fs.readdirSync(dir)[0]);
};

async function approve(deps: Deps): Promise<void> {
  const hash = JSON.parse((await runCli(["profile", "approve", "--json"], deps)).stdout).hash as string;
  const r = await runCli(["profile", "approve", hash], { ...deps, isTTY: true, prompt: async () => hash.slice(0, 6) });
  if (r.exitCode !== 0) throw new Error(r.stderr);
}

describe("sindri observe", () => {
  it("without a profile, observes the current repo read-only", async () => {
    const root = planRepo();
    const d = makeDeps({ cwd: root });
    const r = await runCli(["observe"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("2026-01-01-plan-a.t1");
    expect(r.stdout).toContain("Not recorded: no profile");
    expect(fs.existsSync(ledgerPath(stateDir(d)))).toBe(false);
    expect(fs.existsSync(stateDir(d))).toBe(false);
  });

  it("does not record until the profile is approved, and asks for attention (exit 1)", async () => {
    const deps = await ring0(planRepo());
    const r = await runCli(["observe"], deps);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("has never been approved");
    expect(ledgerState(deps)).toMatchObject({ items: [], events: [] });
  });

  it("records items under the lock once approved, with a would-start reason per row", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    const r = await runCli(["observe"], deps);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(/2026-01-01-plan-a\.t1\s+XS\s+none\s+yes\s+yes\s+0\/1\s+yes\s+Task 1: Small/);
    expect(r.stdout).toMatch(/2026-01-01-plan-a\.t2\s+\?\s+unknown\s+yes\s+no\s+0\/1\s+waits on earlier task/);
    expect(r.stdout).toContain("Observed 2 items (2 open); would start 1.");
    expect(r.stdout).toContain("Next up: 2026-01-01-plan-a.t1");
    expect(r.stdout).toContain("Recorded 2 new, 0 changed, 0 removed in the ledger.");
    const db = openLedger(ledgerPath(stateDir(deps)));
    const epoch = currentEpoch(db);
    expect(epoch).toBe(2); // 1 = the approval, 2 = this observe run
    expect(listItems(db).map((i) => [i.id, i.size, i.epoch])).toEqual([["2026-01-01-plan-a.t1", "XS", epoch], ["2026-01-01-plan-a.t2", null, epoch]]);
    db.close();
    const again = await runCli(["observe", "--json"], deps);
    expect(JSON.parse(again.stdout)).toMatchObject({ observed: 2, open: 2, wouldStart: 1, nextUp: "2026-01-01-plan-a.t1", recorded: { new: 0, changed: 0, removed: 0 } });
  });

  it("records removed tasks and says when nothing is open", async () => {
    const root = planRepo();
    const deps = await ring0(root);
    await approve(deps);
    await runCli(["observe"], deps);
    fs.writeFileSync(path.join(root, PLAN_FILE), PLAN.replace(/### Task 2[\s\S]*$/, "").replace("- [ ] **Step 1: x**", "- [x] **Step 1: x**"));
    const r = await runCli(["observe"], deps);
    expect(r.stdout).toContain("No open items.");
    expect(r.stdout).toContain("Recorded 0 new, 1 changed, 1 removed in the ledger.");
  });

  it("uses the approved snapshot when the live profile has unapproved edits", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    const file = path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("me@example.com", "someone@example.com"));
    const r = await runCli(["observe"], deps);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Using approved profile");
    expect(r.stdout).toContain("would start 1.");
  });

  it("is a no-op recorder when another run holds the lock", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    const lock = path.join(stateDir(deps), "sindri.lock");
    fs.mkdirSync(lock, { recursive: true });
    fs.writeFileSync(path.join(lock, "owner.json"), JSON.stringify({ pid: 1, pidStartTime: "start-1", host: "test-host", bootId: "boot-1", startedAt: "t0", epoch: 5 }));
    const before = ledgerState(deps);
    const r = await runCli(["observe"], deps);
    expect(r.exitCode).toBe(0);
    expect(r.stderr).toBe("no-op: locked by test-host/1 since t0\n");
    expect(r.stdout).toContain("Not recorded: another run holds the lock.");
    expect(ledgerState(deps)).toEqual(before);
    expect(before).toMatchObject({ items: [], events: [] });
  });

  it("does not record on a host that isn't hosts.active (exit 1)", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    const before = ledgerState(deps);
    const r = await runCli(["observe"], { ...deps, system: fakeSystem({ hostname: () => "laptop-2" }) });
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Not recorded: this host (laptop-2) is not hosts.active (test-host).");
    expect(ledgerState(deps)).toEqual(before);
    expect(before).toMatchObject({ items: [], events: [] });
  });

  it("--no-record never writes", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    const before = ledgerState(deps);
    const r = await runCli(["observe", "--no-record"], deps);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Not recorded: --no-record.");
    expect(ledgerState(deps)).toEqual(before);
    expect(before).toMatchObject({ items: [], events: [] });
  });

  it("refuses to record when the plan dir is gone: no removed events, ledger untouched, exit 1", async () => {
    const root = planRepo();
    const deps = await ring0(root);
    await approve(deps);
    expect((await runCli(["observe"], deps)).stdout).toContain("Recorded 2 new");
    const before = ledgerState(deps);
    fs.rmSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
    const r = await runCli(["observe"], deps);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toBe("");
    // The ring-0 repo path is git's resolved top level (/private/var on macOS).
    expect(r.stderr).toContain(`SND-TRACKER-001 plan dir not found: ${path.join(fs.realpathSync(root), "docs/superpowers/plans")}; nothing was recorded`);
    expect(r.stderr).toContain("fix: Restore that directory, or fix `path` in repos/<name>.yaml");
    const after = ledgerState(deps);
    expect(after?.items).toEqual(before?.items);
    expect(after?.events).toEqual(before?.events);
    expect(after?.events.some((e) => (e as { kind: string }).kind === "removed")).toBe(false);
    const json = JSON.parse((await runCli(["observe", "--json"], deps)).stdout);
    expect(json.error.code).toBe("SND-TRACKER-001");
  });

  it("is a no-op (exit 0, no-op: on stderr) when another run takes over mid-run", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    // The tracker reads git inside the lock; bump the epoch then, as a takeover would.
    const git = {
      run: async (args: string[], cwd: string) => {
        if (args[0] === "log") {
          const db = openLedger(ledgerPath(stateDir(deps)));
          bumpEpoch(db);
          db.close();
        }
        return deps.git.run(args, cwd);
      },
    };
    const r = await runCli(["observe"], { ...deps, git });
    expect(r.exitCode).toBe(0);
    expect(r.stderr).toBe("no-op: stale epoch 2 (current 3); another run took over\n");
    expect(r.stdout).toContain("Not recorded: another run took over.");
    expect(ledgerState(deps)).toMatchObject({ items: [], events: [] });
  });

  it("WOULD-START honours the repo's overrides.autoStartMaxSize, else the profile's value", async () => {
    const root = planRepo(PLAN.replace("- Create: `a.ts`", "- Create: `a.ts`\n- Create: `b.ts`"));
    const deps = await ring0(root);
    const profile = path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    const sizeOf = async (): Promise<{ size: string; blocker: string | null }> =>
      JSON.parse((await runCli(["observe", "--json"], deps)).stdout).items[0];
    expect(await sizeOf()).toMatchObject({ size: "S", blocker: "size > XS" }); // core default XS
    fs.appendFileSync(profile, "autoStartMaxSize: S\n");
    expect((await sizeOf()).blocker).toBeNull(); // profile value, no override
    fs.appendFileSync(repoFile(deps), "overrides:\n  autoStartMaxSize: XS\n");
    expect((await sizeOf()).blocker).toBe("size > XS"); // the repo override wins
    fs.writeFileSync(profile, fs.readFileSync(profile, "utf8").replace("autoStartMaxSize: S\n", ""));
    fs.writeFileSync(repoFile(deps), fs.readFileSync(repoFile(deps), "utf8").replace("autoStartMaxSize: XS", "autoStartMaxSize: S"));
    expect((await sizeOf()).blocker).toBeNull(); // override over the default
    expect((await runCli(["profile", "explain", "autoStartMaxSize", "--repo", path.basename(repoFile(deps), ".yaml")], deps)).stdout).toContain('= "S"  (from repos/');
  });

  it("reports an invalid generated ring-0 profile as SND-PROFILE-001", async () => {
    const r = await runCli(["observe"], makeDeps({ cwd: planRepo(), system: fakeSystem({ hostname: () => "" }) }));
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-PROFILE-001");
  });

  it("refuses outside a plan repo when there is no profile", async () => {
    const r = await runCli(["observe"], makeDeps({ cwd: tempDir() }));
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-PROFILE-002");
  });

  it("scrubs secrets out of printed titles", async () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const root = planRepo(PLAN.replace("Task 1: Small", `Task 1: Rotate ${secret}`));
    const r = await runCli(["observe"], makeDeps({ cwd: root }));
    expect(r.stdout).not.toContain(secret);
    expect(r.stdout).toContain("[REDACTED:aws-access-key]");
  });
});

describe("sindri ledger", () => {
  it("says when there is no ledger yet, without creating one", async () => {
    const d = makeDeps();
    expect((await runCli(["ledger"], d)).stdout).toContain("No ledger yet");
    expect(fs.existsSync(ledgerPath(stateDir(d)))).toBe(false);
  });

  it("lists events with filters, and says so when nothing matches", async () => {
    const deps = await ring0(planRepo());
    await approve(deps);
    expect((await runCli(["ledger"], deps)).stdout).toBe("No ledger rows match.\n");
    await runCli(["observe"], deps);
    const all = await runCli(["ledger"], deps);
    expect(all.stdout).toContain("2026-01-01-plan-a.t1  seen");
    const one = JSON.parse((await runCli(["ledger", "--item", "2026-01-01-plan-a.t2", "--since", "7d", "--json"], deps)).stdout);
    expect(one.map((e: { item_id: string }) => e.item_id)).toEqual(["2026-01-01-plan-a.t2"]);
    expect((await runCli(["ledger", "--item", "nope.t9"], deps)).stderr).toContain("SND-ITEM-404");
    expect((await runCli(["ledger", "--since", "soon"], deps)).stderr).toContain("SND-CLI-002");
  });

  it("parseSince reads days and hours", () => {
    const now = new Date("2026-10-08T12:00:00Z");
    expect(parseSince("2d", now).toISOString()).toBe("2026-10-06T12:00:00.000Z");
    expect(parseSince("3h", now).toISOString()).toBe("2026-10-08T09:00:00.000Z");
  });
});
