import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import { makeIndexCommand } from "../src/index/commands.js";
import { indexPath, openIndex } from "../src/index/db.js";
import type { IndexIo } from "../src/index/io.js";
import { makeShapeCommand } from "../src/index/shape.js";
import { spoolDir, writeShapeRun, type ShapeRun } from "../src/index/spool.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { runCli } from "../src/main.js";
import { approvedIndexDeps, BODY, embedFetch, failingGit, fakeIndexIo, OFF, ring0Name, ring0Repo } from "./index-fixtures.js";
import { git, gitRepo, makeDeps, tempDir } from "./helpers.js";

async function ready(o: { index?: string; io?: IndexIo; extraRepos?: (string | { name: string; path: string })[] } = {}): Promise<{ d: Deps; root: string; io: IndexIo }> {
  const root = ring0Repo({ "src/util/text.ts": BODY("clip") });
  // A long budget: these tests check what gets recorded, not timing (shape-budget.test.ts does),
  // so a loaded machine can't turn them into "over budget".
  const index = o.index ?? OFF;
  const d = await approvedIndexDeps(root, { index: index.includes("shape:") ? index : `${index}shape:\n  budgetMs: 60000\n`, extraRepos: o.extraRepos });
  const io = o.io ?? fakeIndexIo();
  await makeIndexCommand(io)(["build", "--repo", ring0Name(d)], d);
  return { d, root, io };
}

const shape = (io: IndexIo = fakeIndexIo()) => makeShapeCommand(io);
const record = (d: Deps, io?: IndexIo, extra: string[] = []) => shape(io)(["--record", "--staged", ...extra], d);
const stage = (cwd: string, file: string, text: string): void => {
  fs.mkdirSync(path.dirname(path.join(cwd, file)), { recursive: true });
  fs.writeFileSync(path.join(cwd, file), text);
  git(cwd, "add", file);
};
const spooled = (d: Deps): ShapeRun[] =>
  fs.readdirSync(spoolDir(d)).filter((n) => n.startsWith("shape-")).sort().map((n) => JSON.parse(fs.readFileSync(path.join(spoolDir(d), n), "utf8")) as ShapeRun);

describe("sindri shape --record --staged", () => {
  it("records signals for a staged clone to the spool, with the tree it will commit, and always exits 0", async () => {
    const { d, root } = await ready();
    stage(root, "src/feature.ts", BODY("shorten"));
    const r = await record(d);
    expect(r.exitCode).toBe(0);
    expect(r.stderr).toBe("sindri-shape: 1 signal(s) recorded (reinvented:exact); record-only, the commit proceeds. See: sindri shape report --recent 1\n");
    const [run] = spooled(d);
    expect(run.signals[0]).toMatchObject({ type: "reinvented:exact", at: "src/feature.ts:1", existing: "src/util/text.ts:1", name: "shorten" });
    expect(run).toMatchObject({ repo: ring0Name(d), tree: git(root, "write-tree").trim(), head: git(root, "rev-parse", "HEAD").trim(), indexAgeMs: 0, providers: { embedder: null, graph: null }, deferred: [] });
  });

  it("records from a linked worktree: matches the profile repo by git common dir and diffs that worktree", async () => {
    const { d, root, io } = await ready();
    const wt = path.join(tempDir("sindri-wt-"), "wt");
    git(root, "worktree", "add", "-q", "-b", "feat", wt);
    stage(wt, "src/feature.ts", BODY("shorten"));
    const r = await record({ ...d, cwd: wt }, io);
    expect(r.stderr).toContain("1 signal(s) recorded (reinvented:exact)");
    expect(spooled(d)[0].signals[0].at).toBe("src/feature.ts:1");
    expect(spooled(d)[0].tree).toBe(git(wt, "write-tree").trim());
  });

  it("under a real hook's GIT_DIR from a linked worktree, matches the worktree's own repo, not the first (Task 9 I2)", async () => {
    const second = ring0Repo({ "src/util/pad.ts": BODY("pad") });
    const { d } = await ready({ extraRepos: [{ name: "zz-second", path: fs.realpathSync(second) }] });
    await makeIndexCommand(fakeIndexIo())(["build", "--repo", "zz-second"], d);
    const wt = path.join(tempDir("sindri-wt-"), "wt");
    git(second, "worktree", "add", "-q", "-b", "feat", wt);
    stage(wt, "src/feature.ts", BODY("shorten"));
    // What git exports to a pre-commit hook in a linked worktree: an absolute GIT_DIR and index.
    const gitDir = git(wt, "rev-parse", "--absolute-git-dir").trim();
    const asHook = async (cwd: string, dir: string) => {
      vi.stubEnv("GIT_DIR", dir);
      vi.stubEnv("GIT_INDEX_FILE", path.join(dir, "index"));
      try {
        return await record({ ...d, cwd });
      } finally {
        vi.unstubAllEnvs();
      }
    };
    expect((await asHook(wt, gitDir)).stderr).toContain("1 signal(s) recorded (reinvented:exact)");
    expect(spooled(d)[0]).toMatchObject({ repo: "zz-second", tree: git(wt, "write-tree").trim() });
    expect(spooled(d)[0].signals[0]).toMatchObject({ at: "src/feature.ts:1", existing: "src/util/pad.ts:1" });

    const outsider = gitRepo({ "a.ts": "export const a = 1;\n" });
    const owt = path.join(tempDir("sindri-wt-"), "owt");
    git(outsider, "worktree", "add", "-q", "-b", "feat", owt);
    stage(owt, "src/feature.ts", BODY("shorten"));
    const odir = git(owt, "rev-parse", "--absolute-git-dir").trim();
    expect((await asHook(owt, odir)).stderr).toBe("sindri-shape: skipped (this repo is not in the profile)\n");
  });

  it("a clone of the profile repo is not the profile repo: it has its own git common dir", async () => {
    const { d, root } = await ready();
    const clone = path.join(tempDir("sindri-clone-"), "c");
    git(root, "clone", "-q", root, clone);
    stage(clone, "src/feature.ts", BODY("shorten"));
    expect((await record({ ...d, cwd: clone })).stderr).toBe("sindri-shape: skipped (this repo is not in the profile)\n");
    expect(fs.existsSync(spoolDir(d))).toBe(false);
  });

  it("asks the embedder within the budget when embeddings are on, and records the provider stamp", async () => {
    const io = fakeIndexIo({ fetch: embedFetch() });
    const { d, root } = await ready({ index: "index:\n  graph: none\n", io });
    stage(root, "src/feature.ts", BODY("shorten"));
    const r = await record(d, io);
    expect(r.stderr).toContain("2 signal(s) recorded (reinvented:exact, reinvented:embedding)");
    expect(spooled(d)[0].providers).toEqual({ embedder: "nomic-embed-text@1", graph: null });
  });

  it("records nothing for a docs-only commit, and a code change with no signals writes a silent run", async () => {
    const { d, root } = await ready();
    stage(root, "README.md", "# hi\n");
    const docs = await record(d);
    expect(docs).toMatchObject({ exitCode: 0, stdout: "", stderr: "" });
    expect(fs.existsSync(spoolDir(d))).toBe(false);
    stage(root, "src/tiny.ts", "export function tiny(a: number) { return a; }\n");
    const tiny = await record(d);
    expect(tiny.stderr).toBe("");
    expect(spooled(d)[0].signals).toEqual([]);
  });

  it("finishes quickly and parses no symbols from a huge generated file (Review Focus 5)", async () => {
    const { d, root } = await ready();
    stage(root, "gen.ts", "export const v = () => 1;\n".repeat(80_000));
    stage(root, "src/feature.ts", BODY("shorten"));
    const t0 = Date.now();
    const r = await record(d);
    expect(Date.now() - t0).toBeLessThan(2000);
    expect(r.stderr).toContain("skipped 1 staged file(s) (denied or over index.maxFileKB)");
    const types = spooled(d)[0].signals.map((s) => s.type).sort();
    expect(types).toEqual(["reinvented:exact", "simpler:diff-size"]);
    expect(spooled(d)[0].signals.every((s) => !s.at.startsWith("gen.ts"))).toBe(true);
  });

  it("deleted-only and renamed-only commits finish, exit 0 and record nothing spurious (Review Focus 5)", async () => {
    const { d, root } = await ready();
    git(root, "rm", "-q", "src/util/text.ts");
    const deleted = await record(d);
    expect(deleted).toMatchObject({ exitCode: 0, stderr: "" });
    expect(fs.existsSync(spoolDir(d))).toBe(false);
    git(root, "reset", "-q", "--hard");
    git(root, "mv", "src/util/text.ts", "src/util/moved.ts");
    const renamed = await record(d, undefined, ["--size", "XL"]);
    expect(renamed).toMatchObject({ exitCode: 0, stderr: "" });
    expect(spooled(d)).toHaveLength(1);
    expect(spooled(d)[0].signals).toEqual([]);
  });

  it("leaves the ledger untouched: no write, no migration, even for an older schema (Review Focus 6)", async () => {
    const { d, root } = await ready();
    stage(root, "src/feature.ts", BODY("shorten"));
    const file = ledgerPath(stateDir(d));
    const before = fs.readFileSync(file);
    // The ledger and whatever sits beside it (-wal, -shm, .bak-*): nothing new, nothing gone.
    const beside = (): string[] => fs.readdirSync(path.dirname(file)).filter((n) => n.startsWith(path.basename(file))).sort();
    const listing = beside();
    expect((await record(d)).stderr).toContain("1 signal(s) recorded");
    expect(fs.readFileSync(file)).toEqual(before);
    expect(beside()).toEqual(listing);
    const raw = new Database(file);
    raw.pragma("user_version = 1");
    raw.close();
    const r = await record(d);
    expect(r.stderr).toContain("sindri-shape: skipped (no approved profile)");
    expect(beside()).toEqual(listing);
    const check = new Database(file, { readonly: true });
    expect(check.pragma("user_version", { simple: true })).toBe(1);
    check.close();
  });

  it("never fails a commit: every skip is exit 0 with a note", async () => {
    expect((await record(makeDeps())).stderr).toBe("sindri-shape: skipped (no approved profile)\n");
    const root = ring0Repo({ "a.ts": "export const a = 1;\n" });
    const unapproved = makeDeps({ cwd: root });
    await runCli(["profile", "init", "--ring0"], unapproved);
    openLedger(ledgerPath(stateDir(unapproved))).close();
    expect((await record(unapproved)).stderr).toBe("sindri-shape: skipped (no approved profile)\n");

    const off = await ready({ index: `${OFF}shape:\n  record: false\n` });
    stage(off.root, "src/feature.ts", BODY("shorten"));
    expect(await record(off.d)).toMatchObject({ exitCode: 0, stderr: "" });
    expect(fs.existsSync(spoolDir(off.d))).toBe(false);

    const { d, root: repoRoot } = await ready({ extraRepos: ["ghost"] });
    stage(repoRoot, "src/feature.ts", BODY("shorten"));
    expect((await record({ ...d, cwd: tempDir() })).stderr).toBe("sindri-shape: skipped (not inside a git repo)\n");
    expect((await record({ ...d, cwd: gitRepo({ "a.ts": "x" }) })).stderr).toBe("sindri-shape: skipped (this repo is not in the profile)\n");
    expect((await record(d, undefined, ["--repo", "nope"])).stderr).toBe("sindri-shape: skipped (no repo named nope in the profile)\n");

    const fresh = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }));
    expect((await record(fresh)).stderr).toBe("sindri-shape: skipped (no index; sindri index build)\n");
  });

  it("copes with a failing HEAD or write-tree, an index that was never built, and an unwritable spool", async () => {
    const { d, root } = await ready();
    stage(root, "src/feature.ts", BODY("shorten"));
    await record({ ...d, git: failingGit("rev-parse HEAD", "write-tree") });
    expect(spooled(d)[0]).toMatchObject({ head: null, tree: null });

    const never = await ready();
    fs.rmSync(indexPath(never.d, ring0Name(never.d)));
    openIndex(indexPath(never.d, ring0Name(never.d))).close();
    stage(never.root, "src/feature.ts", BODY("shorten"));
    await record(never.d);
    expect(spooled(never.d)[0].indexAgeMs).toBeNull();

    const blocked = await ready();
    stage(blocked.root, "src/feature.ts", BODY("shorten"));
    fs.mkdirSync(path.dirname(spoolDir(blocked.d)), { recursive: true });
    fs.writeFileSync(spoolDir(blocked.d), "a file where the spool dir should be");
    const r = await record(blocked.d);
    expect(r.exitCode).toBe(0);
    expect(r.stderr).toMatch(/^sindri-shape: skipped \(.+\)\n$/);
  });

  it("rejects an unknown shape subcommand", async () => {
    expect((await shape()(["frob"], makeDeps())).stderr).toContain("SND-CLI-002");
    expect((await shape()([], makeDeps())).stderr).toContain("unknown shape subcommand: (none)");
  });
});

describe("sindri shape report", () => {
  const diffRun = (d: Deps): void => {
    writeShapeRun(d, {
      runId: "01k0000000000000000000000z", repo: ring0Name(d), ts: "2026-10-08T12:00:00.000Z", head: null, tree: null, elapsedMs: 5, indexAgeMs: null,
      providers: { embedder: null, graph: null }, deferred: [],
      signals: [{ type: "simpler:diff-size", layer: "structure", value: 400, threshold: 250, at: "(diff)", existing: null, detail: "400 added lines; the size budget is 250", name: null, astHash: null }],
    });
  };

  it("ingests the spool, prints the per-type table with a header, and lists recent signals with their evidence", async () => {
    const { d, root } = await ready();
    stage(root, "src/feature.ts", BODY("shorten"));
    await record(d);
    diffRun(d);
    const r = await shape()(["report", "--recent", "2"], d);
    expect(r.stdout).toContain("Ingested 2 run(s), 2 signal(s).");
    expect(r.stdout).toContain("Runs: 2 recorded; 0 deferred the embeddings layer.");
    expect(r.stdout).toMatch(/^TYPE\s+SIGNALS\s+LABELED\s+ACTED-ON\s+KEPT\s+PRECISION\s+TOWARD 3b$/m);
    expect(r.stdout).toMatch(/^reinvented:exact\s+1\s+0\s+0\s+0\s+n\/a\s+0\/30 labeled; bar 0\.70$/m);
    expect(r.stdout).toContain("simpler:diff-size  (diff) vs -  value 400/250  index age unknown  unlabeled");
    expect(r.stdout).toContain("reinvented:exact  src/feature.ts:1 vs src/util/text.ts:1  value 1/1  index 0 h old  unlabeled");
    expect(r.stdout).toContain("    <untrusted>shorten</untrusted> has the same structure as <untrusted>clip</untrusted>");
    expect(spooled(d)).toEqual([]);
    const json = JSON.parse((await shape()(["report", "--json"], d)).stdout);
    expect(json.byType["reinvented:exact"]).toBe(1);
    expect((await shape()(["report", "--recent", "0"], d)).stderr).toContain("SND-CLI-002");
  });

  it("says when nothing was recorded, notes quarantined files, and notes a held tick lock", async () => {
    const { d } = await ready();
    expect((await shape()(["report"], d)).stdout).toContain("No shape signals recorded yet.");
    fs.mkdirSync(spoolDir(d), { recursive: true });
    fs.writeFileSync(path.join(spoolDir(d), "shape-garbage.json"), "not json");
    expect((await shape()(["report"], d)).stdout).toContain("Quarantined 1 bad spool file(s).");
    diffRun(d);
    const db = openLedger(ledgerPath(stateDir(d)));
    const held = acquireTickLock({ dir: stateDir(d), db, sys: d.system, now: d.now });
    const busy = await shape()(["report"], d);
    if (held.ok) held.release();
    db.close();
    expect(busy.stdout).toContain("Ingested 0 run(s), 0 signal(s). (Another run holds the lock; showing what's already ingested.)");
    expect(fs.existsSync(path.join(spoolDir(d), "shape-01k0000000000000000000000z.json"))).toBe(true);
  });

  it("observe moves the spool into the ledger while it records", async () => {
    const { d, root } = await ready();
    stage(root, "src/feature.ts", BODY("shorten"));
    await record(d);
    const r = await runCli(["observe"], d);
    expect(r.stdout).toContain("Recorded");
    const db = openLedger(ledgerPath(stateDir(d)));
    expect(db.prepare("SELECT COUNT(*) AS n FROM shape_signals").get()).toEqual({ n: 1 });
    db.close();
    expect(spooled(d)).toEqual([]);
  });
});
