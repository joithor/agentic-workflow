import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { trackerContractTests } from "./contract/tracker-contract.js";
import { makePlanFileTracker, parseGitHistory, planItemId, wildcard } from "../src/adapters/plan-file/tracker.js";
import { makeTracker } from "../src/adapters/registry.js";
import { realGitRunner } from "../src/git-real.js";
import { loadProfile } from "../src/profile/load.js";
import { fakeGit, makeDeps, tempDir } from "./helpers.js";

const PLAN = (n: number, done: boolean) =>
  [`# Plan ${n}`, "", "### Task 1: Alpha", "", "**Files:**", "- Create: `a.ts`", "", `- [${done ? "x" : " "}] **Step 1: do it**`, "", "```ts", "x", "```", "", "### Task 2: Beta", "", "- [ ] **Step 1: later**", ""].join("\n");

function git(root: string, ...args: string[]): void {
  execFileSync("git", ["-c", "user.name=Tester", "-c", "user.email=tester@example.com", ...args], { cwd: root, stdio: "ignore" });
}

function repo(): string {
  const root = tempDir("sindri-plans-");
  fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
  fs.writeFileSync(path.join(root, "docs/superpowers/plans/2026-01-01-plan-a.md"), PLAN(1, true));
  fs.writeFileSync(path.join(root, "docs/superpowers/plans/2026-01-02-plan-b.md"), PLAN(2, false));
  fs.writeFileSync(path.join(root, "docs/superpowers/plans/notes.txt"), "ignored");
  git(root, "init", "-q");
  git(root, "add", ".");
  git(root, "commit", "-qm", "plans");
  return root;
}

const GLOB = "docs/superpowers/plans/*.md";

trackerContractTests("plan-file", async () => {
  const root = repo();
  return {
    tracker: makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*"], git: realGitRunner() }),
    touch: async (id) => {
      const file = path.join(root, "docs/superpowers/plans", `${id.split(".t")[0]}.md`);
      fs.writeFileSync(file, fs.readFileSync(file, "utf8").replace("later", "later, edited"));
    },
  };
});

describe("plan-file tracker", () => {
  it("turns plan tasks into work items with authors, state and meta", async () => {
    const root = repo();
    const t = makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*"], git: realGitRunner() });
    const scan = await t.scan({ includeDone: true });
    expect(scan.ok && scan.value.items.map((i) => i.id)).toEqual(["2026-01-01-plan-a.t1", "2026-01-01-plan-a.t2", "2026-01-02-plan-b.t1", "2026-01-02-plan-b.t2"]);
    const a1 = await t.read("2026-01-01-plan-a.t1");
    expect(a1.ok && a1.value).toMatchObject({
      title: "Task 1: Alpha (Plan 1)",
      url: "docs/superpowers/plans/2026-01-01-plan-a.md#task-1",
      state: "done",
      authors: [{ id: "tester@example.com", role: "creator" }],
      meta: { plan: "2026-01-01-plan-a", task: 1, order: 1, stepsDone: 1, stepsTotal: 1, files: 1, codeLines: 1, hasFilesBlock: 1 },
    });
    const open = await t.scan({ includeDone: false });
    expect(open.ok && open.value.items).toHaveLength(3);
    expect(planItemId("docs/x/2026-01-01-plan-a.md", 3)).toBe("2026-01-01-plan-a.t3");
  });

  it("falls back to the file mtime and no authors when git has no history, and rescans on a bad cursor", async () => {
    const root = tempDir();
    fs.mkdirSync(path.join(root, "docs/superpowers/plans"), { recursive: true });
    fs.writeFileSync(path.join(root, "docs/superpowers/plans/p.md"), PLAN(1, false));
    const t = makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*"], git: fakeGit({}) });
    const r = await t.read("p.t1");
    expect(r.ok && r.value.authors).toEqual([]);
    expect(r.ok && r.value.updatedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    for (const bad of ["not-a-cursor", Buffer.from("42").toString("base64url")]) {
      const scan = await t.scan({ includeDone: true }, bad);
      expect(scan.ok && scan.value.items).toHaveLength(2);
    }
  });

  it("reads only included plan files, skips non-files, and marks editors", async () => {
    const root = repo();
    fs.mkdirSync(path.join(root, "docs/superpowers/plans/dir.md"));
    const plan = path.join(root, "docs/superpowers/plans/2026-01-02-plan-b.md");
    fs.appendFileSync(plan, "\n");
    execFileSync("git", ["-c", "user.name=E", "-c", "user.email=editor@example.com", "commit", "-qam", "edit"], { cwd: root });
    const t = makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*-plan-b"], git: realGitRunner() });
    const none = await t.scan({ includeDone: true });
    expect(none.ok && none.value.items).toEqual([]);
    const only = makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*-plan-b.md"], git: realGitRunner() });
    const scan = await only.scan({ includeDone: true });
    expect(scan.ok && scan.value.items.map((i) => i.id)).toEqual(["2026-01-02-plan-b.t1", "2026-01-02-plan-b.t2"]);
    const b1 = await only.read("2026-01-02-plan-b.t1");
    expect(b1.ok && b1.value.authors).toEqual([
      { id: "editor@example.com", role: "editor" },
      { id: "tester@example.com", role: "creator" },
    ]);
    expect(b1.ok && String(b1.value.meta.contentHash)).toMatch(/^[0-9a-f]{64}$/);
  });

  it("skips directories whose names look like plan files", async () => {
    const root = repo();
    fs.mkdirSync(path.join(root, "docs/superpowers/plans/dir.md"));
    const t = makePlanFileTracker({ repoPath: root, glob: GLOB, include: ["*"], git: realGitRunner() });
    const scan = await t.scan({ includeDone: true });
    expect(scan.ok && scan.value.items.map((i) => i.id)).toEqual(["2026-01-01-plan-a.t1", "2026-01-01-plan-a.t2", "2026-01-02-plan-b.t1", "2026-01-02-plan-b.t2"]);
  });

  it("parses one git log for the whole plan directory", () => {
    const out = "\x1enew@example.com\t2026-02-02T00:00:00Z\n\nplans/a.md\n\x1eold@example.com\t2026-01-01T00:00:00Z\n\nplans/a.md\nplans/b.md\n";
    const h = parseGitHistory(out);
    expect(h.get("plans/a.md")).toEqual({ authors: ["new@example.com", "old@example.com"], date: "2026-02-02T00:00:00Z" });
    expect(h.get("plans/b.md")).toEqual({ authors: ["old@example.com"], date: "2026-01-01T00:00:00Z" });
    expect(wildcard("*-plan-*.md").test("2026-01-01-sindri-plan-2-core.md")).toBe(true);
    expect(wildcard("a.md").test("aXmd")).toBe(false);
  });

  it("is empty when the plan dir doesn't exist", async () => {
    const t = makePlanFileTracker({ repoPath: tempDir(), glob: GLOB, include: ["*"], git: fakeGit({}) });
    const scan = await t.scan({ includeDone: true });
    expect(scan.ok && scan.value.items).toEqual([]);
  });

  it("makeTracker builds the profile's tracker", async () => {
    const root = repo();
    const dir = tempDir();
    fs.mkdirSync(path.join(dir, "repos"));
    fs.writeFileSync(path.join(dir, "profile.yaml"), "schemaVersion: 1\nuser: me\nhosts:\n  active: h\ntracker:\n  type: plan-file\n  repo: r\nrepos:\n  - r\n");
    fs.writeFileSync(path.join(dir, "repos/r.yaml"), `schemaVersion: 1\nname: r\npath: ${root}\n`);
    const loaded = loadProfile(dir);
    if (!loaded.ok) throw new Error(JSON.stringify(loaded.issues));
    const t = makeTracker(loaded.value, makeDeps());
    const scan = await t.scan({ includeDone: true });
    expect(scan.ok && scan.value.items).toHaveLength(4);
  });
});
