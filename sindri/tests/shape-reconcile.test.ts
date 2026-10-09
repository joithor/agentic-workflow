import { execFileSync } from "node:child_process";
import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir, type Deps } from "../src/deps.js";
import { makeIndexCommand } from "../src/index/commands.js";
import { indexPath } from "../src/index/db.js";
import { parserId, typescriptParser } from "../src/index/parse-ts.js";
import { FETCH_ENV, reconcileShape } from "../src/index/reconcile.js";
import { makeShapeCommand } from "../src/index/shape.js";
import { bumpEpoch, ledgerPath, openLedger, schemaVersion, type Ledger } from "../src/ledger/db.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { approvedProfile } from "../src/profile/approve.js";
import type { LoadedProfile } from "../src/profile/load.js";
import { runCli } from "../src/main.js";
import { approvedIndexDeps, BODY, failingGit, fakeIndexIo, ring0Name, ring0Repo } from "./index-fixtures.js";
import type { GitRunner } from "../src/git.js";
import { realGitRunner } from "../src/git-real.js";
import { git, makeDeps, tempDir } from "./helpers.js";

const TS = "2026-10-08T12:00:00.000Z"; // the fixed commit date of every test commit
const later = (d: Deps, days: number): Deps => ({ ...d, now: () => new Date(Date.parse(TS) + days * 86_400_000) });

interface Sig { type: string; at: string; name: string | null; hash: string | null; outcome?: string }
function insertRun(db: Ledger, o: { id: string; repo: string; tree: string | null; sha?: string; ts?: string; deferred?: string; parser?: string; signals: Sig[] }): void {
  db.prepare(
    "INSERT INTO shape_runs (run_id, repo, ts, head, tree, commit_sha, elapsed_ms, index_age_ms, providers, parser, deferred, signal_count, epoch) VALUES (?, ?, ?, NULL, ?, ?, 0, NULL, '{}', ?, ?, ?, 1)",
  ).run(o.id, o.repo, o.ts ?? TS, o.tree, o.sha ?? null, o.parser ?? parserId(), o.deferred ?? "[]", o.signals.length);
  for (const s of o.signals) {
    db.prepare("INSERT INTO shape_signals (run_id, type, layer, value, threshold, at, existing, detail, name, ast_hash, outcome, epoch) VALUES (?, ?, 'clones', 1, 1, ?, NULL, 'd', ?, ?, ?, 1)").run(
      o.id, s.type, s.at, s.name, s.hash, s.outcome ?? null,
    );
  }
}

const outcomes = (db: Ledger): Record<string, string | null> =>
  Object.fromEntries((db.prepare("SELECT run_id, type, name, outcome FROM shape_signals ORDER BY seq").all() as { run_id: string; type: string; name: string | null; outcome: string | null }[]).map((r) => [`${r.run_id}|${r.type}|${r.name}`, r.outcome]));

// The ast hash the parser gives BODY(name) in `file`: what a signal records for a kept symbol.
const hashOf = (file: string, name: string): string => typescriptParser.parse(file, BODY(name))[0].astHash;

// A repo with this history (every commit dated TS):
//   c1 (default branch): feature.ts `shorten`, retired.ts `retired`, package.json {dayjs, moment}, legacy/package.json {left-pad}
//   feature     (side branch, never merged): c1 + side.ts `sidefn`
//   squash-src  (side branch, never merged): c1 + squashed.ts `squashed`
//   c2 (default branch): removes retired.ts, moment and legacy/package.json
//   c3 (default branch): a squash merge, a NEW commit that adds squashed.ts with squash-src's content
// and `feature` is left checked out. Runs: run-a is c1's, run-b the unmerged side.ts's, run-s the
// squash branch's, run-c has no matching commit; the rest can't be linked or labeled ("ghost" is a
// profile repo whose path isn't a git repo; "removed" isn't in the profile).
async function world(): Promise<{ d: Deps; db: Ledger; loaded: LoadedProfile; root: string; name: string; branch: string; first: string }> {
  const root = ring0Repo({
    "src/util/text.ts": BODY("clip"),
    "src/feature.ts": BODY("shorten"),
    "src/retired.ts": BODY("retired"),
    "package.json": JSON.stringify({ dependencies: { dayjs: "^1", moment: "^2" } }),
    "legacy/package.json": JSON.stringify({ dependencies: { "left-pad": "^1" } }),
  });
  const d = await approvedIndexDeps(root, { extraRepos: ["ghost"] });
  const name = ring0Name(d);
  const branch = git(root, "symbolic-ref", "--short", "HEAD").trim();
  const first = git(root, "rev-parse", "HEAD").trim();
  const firstTree = git(root, "rev-parse", "HEAD^{tree}").trim();
  const write = (rel: string, text: string | null): void => {
    const file = path.join(root, rel);
    if (text === null) fs.rmSync(file);
    else {
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, text);
    }
  };
  const commit = (msg: string): string => {
    git(root, "add", "-A");
    git(root, "commit", "-qm", msg);
    return git(root, "rev-parse", "HEAD^{tree}").trim();
  };
  git(root, "checkout", "-q", "-b", "feature");
  write("src/side.ts", BODY("sidefn"));
  const sideTree = commit("side");
  git(root, "checkout", "-q", branch);
  git(root, "checkout", "-q", "-b", "squash-src");
  write("src/squashed.ts", BODY("squashed"));
  const squashTree = commit("squash source");
  git(root, "checkout", "-q", branch);
  write("src/retired.ts", null);
  write("legacy/package.json", null);
  write("package.json", JSON.stringify({ dependencies: { dayjs: "^1" } }));
  commit("retire");
  git(root, "checkout", "-q", "squash-src", "--", "src/squashed.ts");
  commit("squash merge");
  git(root, "checkout", "-q", "feature");
  const db = openLedger(ledgerPath(stateDir(d)));
  insertRun(db, {
    id: "run-a", repo: name, tree: firstTree,
    signals: [
      { type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: hashOf("src/feature.ts", "shorten") },
      { type: "reinvented:exact", at: "src/retired.ts:1", name: "retired", hash: hashOf("src/retired.ts", "retired") },
      { type: "generalize:near-clone", at: "src/feature.ts:1", name: "shorten", hash: "f".repeat(64) },
      { type: "reinvented:dependency", at: "package.json", name: "moment", hash: null },
      { type: "reinvented:dependency", at: "package.json", name: "dayjs", hash: null },
      { type: "reinvented:dependency", at: "legacy/package.json", name: "left-pad", hash: null },
      { type: "simpler:diff-size", at: "(diff)", name: null, hash: null },
      { type: "simpler:exports", at: "(diff)", name: null, hash: null },
      { type: "reinvented:name", at: "src/feature.ts:1", name: null, hash: null },
      { type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: null },
    ],
  });
  insertRun(db, { id: "run-b", repo: name, tree: sideTree, signals: [{ type: "reinvented:exact", at: "src/side.ts:1", name: "sidefn", hash: hashOf("src/side.ts", "sidefn") }] });
  insertRun(db, {
    id: "run-s", repo: name, tree: squashTree,
    signals: [
      { type: "reinvented:exact", at: "src/squashed.ts:1", name: "squashed", hash: hashOf("src/squashed.ts", "squashed") },
      { type: "simpler:complexity", at: "src/squashed.ts:1", name: "squashed", hash: "f".repeat(64) },
    ],
  });
  insertRun(db, { id: "run-c", repo: name, tree: "c".repeat(40), deferred: '["embeddings"]', signals: [{ type: "reinvented:exact", at: "src/x.ts:1", name: "x", hash: "a".repeat(64) }] });
  insertRun(db, { id: "run-g", repo: "ghost", tree: "d".repeat(40), signals: [{ type: "reinvented:exact", at: "g.ts:1", name: "g", hash: "a".repeat(64) }] });
  insertRun(db, { id: "run-r", repo: "removed", tree: "e".repeat(40), signals: [{ type: "reinvented:exact", at: "r.ts:1", name: "r", hash: "a".repeat(64) }] });
  insertRun(db, { id: "run-p", repo: "removed", tree: null, sha: "f".repeat(40), signals: [{ type: "reinvented:exact", at: "p.ts:1", name: "p", hash: "a".repeat(64) }] });
  insertRun(db, { id: "run-q", repo: "ghost", tree: null, sha: "f".repeat(40), signals: [{ type: "reinvented:exact", at: "q.ts:1", name: "q", hash: "a".repeat(64) }] });
  const loaded = approvedProfile(d, db);
  if (loaded === null) throw new Error("profile is not approved");
  return { d, db, loaded, root, name, branch, first };
}

// Link at day 2, label at day 15, from a world that `setup` may have rearranged first.
async function labelsAfter(setup: (root: string, branch: string, first: string) => void = () => undefined): Promise<Record<string, string | null>> {
  const { d, db, loaded, root, branch, first } = await world();
  setup(root, branch, first);
  await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db));
  await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db));
  const labels = outcomes(db);
  db.close();
  return labels;
}

const EXPECTED: Record<string, string | null> = {
  "run-a|reinvented:exact|shorten": "kept", // still on the default branch with the recorded ast hash
  "run-a|reinvented:exact|retired": "acted-on", // added on the default branch, then its file was deleted
  "run-a|generalize:near-clone|shorten": "acted-on", // same name, different ast hash
  "run-a|reinvented:dependency|moment": "acted-on", // added, then removed from the manifest
  "run-a|reinvented:dependency|dayjs": "kept",
  "run-a|reinvented:dependency|left-pad": "acted-on", // the whole manifest was deleted
  "run-a|simpler:diff-size|null": "n/a",
  "run-a|simpler:exports|null": "n/a",
  "run-a|reinvented:name|null": "n/a",
  "run-a|simpler:complexity|shorten": "n/a", // no recorded hash: nothing can be matched, so never acted-on
  "run-b|reinvented:exact|sidefn": "dropped", // only ever on the unmerged side branch
  "run-s|reinvented:exact|squashed": "kept", // squash merge: a new sha, same content, still counts as merged
  "run-s|simpler:complexity|squashed": "dropped", // no version on the default branch ever held this hash
  "run-c|reinvented:exact|x": "dropped", // no commit ever matched, and it is older than 7 days
  "run-g|reinvented:exact|g": null, // not a git repo
  "run-r|reinvented:exact|r": null, // repo not in the profile
  "run-p|reinvented:exact|p": null,
  "run-q|reinvented:exact|q": null,
};

describe("reconcileShape (Review Focus 7)", () => {
  it("takes the default branch from git symbolic-ref, and the world leaves a side branch checked out", async () => {
    const { db, loaded, root, name, branch } = await world();
    expect(loaded.repos[name].defaultBranch).toBe(branch);
    expect(branch).toBe("main");
    expect(git(root, "symbolic-ref", "--short", "HEAD").trim()).toBe("feature");
    db.close();
  });

  it("links runs to commits by tree, and waits until a commit is old enough to label", async () => {
    const { d, db, loaded, root, first } = await world();
    expect(await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db))).toEqual({ linked: 3, labeled: 0 });
    expect(db.prepare("SELECT run_id FROM shape_runs WHERE commit_sha IS NOT NULL ORDER BY run_id").all()).toEqual([{ run_id: "run-a" }, { run_id: "run-b" }, { run_id: "run-p" }, { run_id: "run-q" }, { run_id: "run-s" }]);
    expect(Object.values(outcomes(db)).every((o) => o === null)).toBe(true);
    expect(db.prepare("SELECT run_id, commit_sha FROM shape_runs WHERE run_id IN ('run-a', 'run-b', 'run-s') ORDER BY run_id").all()).toEqual([
      { run_id: "run-a", commit_sha: first },
      { run_id: "run-b", commit_sha: git(root, "rev-parse", "feature").trim() },
      { run_id: "run-s", commit_sha: git(root, "rev-parse", "squash-src").trim() },
    ]);
    db.close();
  });

  it("labels acted-on, kept, dropped (unmerged side branch or never made), a squash merge, dependencies and n/a after 15 days", async () => {
    const { d, db, loaded } = await world();
    await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db));
    expect(await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db))).toEqual({ linked: 0, labeled: 14 });
    expect(outcomes(db)).toEqual(EXPECTED);
    expect(await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db))).toEqual({ linked: 0, labeled: 0 });
    db.close();
  });

  it("makes no git call when no run waits for its commit and no signal is due (most hourly ticks)", async () => {
    const x = await dated();
    const tree = git(x.root, "rev-parse", "HEAD^{tree}").trim();
    insertRun(x.db, { id: "run-1", repo: x.name, tree, signals: [{ type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: hashOf("src/feature.ts", "shorten") }] });
    await reconcileShape(x.db, later(x.d, 1), x.loaded, bumpEpoch(x.db)); // links it
    const calls: string[][] = [];
    const spy: GitRunner = { run: async (args, cwd, o) => (calls.push(args), realGitRunner().run(args, cwd, o)) };
    expect(await reconcileShape(x.db, { ...later(x.d, 2), git: spy }, x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 0 });
    expect(calls).toEqual([]);
    x.db.close();
  });

  it("gives the same labels with the default branch checked out and uncommitted edits in the working tree", async () => {
    const labels = await labelsAfter((root, branch) => {
      git(root, "checkout", "-q", branch);
      fs.writeFileSync(path.join(root, "src/feature.ts"), "// work in progress\n");
    });
    expect(labels).toEqual(EXPECTED);
  });

  it("reads origin/<default> when the local default branch is stale", async () => {
    const labels = await labelsAfter((root, branch, first) => {
      git(root, "update-ref", `refs/remotes/origin/${branch}`, branch);
      git(root, "branch", "-f", branch, first);
    });
    expect(labels).toEqual(EXPECTED);
  });

  it("labels only the n/a signals when git cannot search the default branch", async () => {
    const { d, db, loaded } = await world();
    await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db));
    expect(await reconcileShape(db, { ...later(d, 15), git: failingGit("merge-base", " -- ") }, loaded, bumpEpoch(db))).toEqual({ linked: 0, labeled: 5 });
    const labels = outcomes(db);
    expect(Object.entries(labels).filter(([, o]) => o !== null).map(([k, o]) => `${k}=${o}`)).toEqual([
      "run-a|simpler:diff-size|null=n/a",
      "run-a|simpler:exports|null=n/a",
      "run-a|reinvented:name|null=n/a",
      "run-a|simpler:complexity|shorten=n/a",
      "run-c|reinvented:exact|x=dropped",
    ]);
    db.close();
  });
});

// Review Focus 7: a signal that can never be labeled is labeled dropped or left unlabeled, never
// mislabeled; a repo whose default branch can't be read right now is skipped, not guessed at.
describe("reconcileShape: signals that can't be labeled (Review Focus 7)", () => {
  const labeledOnly = (labels: Record<string, string | null>): string[] => Object.entries(labels).filter(([, o]) => o !== null).map(([k, o]) => `${k}=${o}`);

  it("labels without an index: none is built, and the labels don't need one", async () => {
    const { d, db, loaded, name } = await world();
    expect(fs.existsSync(indexPath(d, name))).toBe(false);
    await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db));
    await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db));
    expect(outcomes(db)).toEqual(EXPECTED);
    db.close();
  });

  it("drops an amended commit's run once it is outcomeDays old: its staged tree never became a reachable commit", async () => {
    const root = ring0Repo({ "src/util/text.ts": BODY("clip") });
    const d = await approvedIndexDeps(root);
    fs.writeFileSync(path.join(root, "src/feature.ts"), BODY("first"));
    git(root, "add", "-A");
    const staged = git(root, "write-tree").trim();
    git(root, "commit", "-qm", "first");
    fs.writeFileSync(path.join(root, "src/feature.ts"), BODY("second"));
    git(root, "add", "-A");
    git(root, "commit", "-q", "--amend", "-m", "amended");
    const db = openLedger(ledgerPath(stateDir(d)));
    insertRun(db, { id: "run-m", repo: ring0Name(d), tree: staged, signals: [{ type: "reinvented:exact", at: "src/feature.ts:1", name: "first", hash: hashOf("src/feature.ts", "first") }] });
    const loaded = approvedProfile(d, db);
    if (loaded === null) throw new Error("profile is not approved");
    expect(await reconcileShape(db, later(d, 14), loaded, bumpEpoch(db))).toEqual({ linked: 0, labeled: 0 });
    expect(outcomes(db)).toEqual({ "run-m|reinvented:exact|first": null });
    expect(await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db))).toEqual({ linked: 0, labeled: 1 });
    expect(db.prepare("SELECT commit_sha FROM shape_runs").get()).toEqual({ commit_sha: null });
    expect(outcomes(db)).toEqual({ "run-m|reinvented:exact|first": "dropped" });
    db.close();
  });

  it("leaves a repo unlabeled when it has an origin and the fetch fails (offline), and never touches the network to find out", async () => {
    const labels = await labelsAfter((root) => {
      git(root, "remote", "add", "origin", path.join(tempDir("sindri-offline-"), "missing.git"));
    });
    expect(labeledOnly(labels)).toEqual(["run-c|reinvented:exact|x=dropped"]);
  });

  it("fetches a reachable origin without moving local branches or HEAD, then labels from origin/<default>", async () => {
    const { d, db, loaded, root, branch, first } = await world();
    const bare = path.join(tempDir("sindri-origin-"), "origin.git");
    git(root, "clone", "-q", "--bare", root, bare);
    git(root, "remote", "add", "origin", bare);
    git(root, "branch", "-f", branch, first); // the local default branch is stale; origin is not
    const heads = git(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads");
    await reconcileShape(db, later(d, 2), loaded, bumpEpoch(db));
    await reconcileShape(db, later(d, 15), loaded, bumpEpoch(db));
    expect(outcomes(db)).toEqual(EXPECTED);
    expect(git(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads")).toBe(heads);
    expect(git(root, "symbolic-ref", "--short", "HEAD").trim()).toBe("feature");
    db.close();
  });

  it("leaves a repo unlabeled when neither origin/<default> nor the local default branch resolves", async () => {
    const labels = await labelsAfter((root, branch) => {
      git(root, "branch", "-m", branch, "trunk");
    });
    expect(labeledOnly(labels)).toEqual(["run-c|reinvented:exact|x=dropped"]);
  });

  it("computes precision only from acted-on and kept: dropped and n/a never count", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }));
    const db = openLedger(ledgerPath(stateDir(d)));
    insertRun(db, {
      id: "run-1", repo: ring0Name(d), tree: null,
      signals: [
        { type: "reinvented:exact", at: "a.ts:1", name: "a", hash: null, outcome: "dropped" },
        { type: "reinvented:exact", at: "a.ts:2", name: "b", hash: null, outcome: "n/a" },
        { type: "reinvented:exact", at: "a.ts:3", name: "c", hash: null, outcome: "acted-on" },
        { type: "reinvented:name", at: "a.ts:4", name: "d", hash: null, outcome: "dropped" },
      ],
    });
    db.close();
    const json = JSON.parse((await makeShapeCommand(fakeIndexIo())(["report", "--json"], d)).stdout);
    expect(json.types.find((t: { key: string }) => t.key === "reinvented:exact")).toMatchObject({ signals: 3, labeled: 1, acted: 1, kept: 0, precision: 1 });
    expect(json.types.find((t: { key: string }) => t.key === "reinvented:name")).toMatchObject({ signals: 1, labeled: 0, precision: null });
  });
});

// Realistic history: each commit carries its own date, days after TS, so a name first added
// long before a run is outside any window that starts at the run.
const dayIso = (days: number): string => new Date(Date.parse(TS) + days * 86_400_000).toISOString();
function gitOn(days: number, cwd: string, ...args: string[]): string {
  const date = dayIso(days);
  return execFileSync("git", ["-c", "user.name=Tester", "-c", "user.email=tester@example.com", ...args], {
    cwd, encoding: "utf8", env: { ...process.env, GIT_AUTHOR_DATE: date, GIT_COMMITTER_DATE: date },
  });
}

// One approved repo whose first commit (at TS) has src/util/text.ts `clip` and src/feature.ts `shorten`.
async function dated(): Promise<{ d: Deps; db: Ledger; loaded: LoadedProfile; root: string; name: string; put: (rel: string, text: string) => void; commitOn: (days: number, msg: string) => string }> {
  const root = ring0Repo({ "src/util/text.ts": BODY("clip"), "src/feature.ts": BODY("shorten") });
  const d = await approvedIndexDeps(root);
  const db = openLedger(ledgerPath(stateDir(d)));
  const loaded = approvedProfile(d, db);
  if (loaded === null) throw new Error("profile is not approved");
  const put = (rel: string, text: string): void => {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  };
  const commitOn = (days: number, msg: string): string => {
    gitOn(days, root, "add", "-A");
    gitOn(days, root, "commit", "-qm", msg);
    return git(root, "rev-parse", "HEAD^{tree}").trim();
  };
  return { d, db, loaded, root, name: ring0Name(d), put, commitOn };
}

async function labelOn(x: { d: Deps; db: Ledger; loaded: LoadedProfile }, linkDay: number, labelDay: number): Promise<Record<string, string | null>> {
  await reconcileShape(x.db, later(x.d, linkDay), x.loaded, bumpEpoch(x.db));
  await reconcileShape(x.db, later(x.d, labelDay), x.loaded, bumpEpoch(x.db));
  return outcomes(x.db);
}

describe("reconcileShape: reached the default branch, judged by content (Task 10 ruling)", () => {
  // Edits that change the body's structure, not only a literal (the ast hash ignores literals).
  const EDITED = BODY("shorten", "out.reverse();");
  const AGAIN = BODY("shorten", "out.reverse(); out.sort();");
  const editedHash = (): string => typescriptParser.parse("src/feature.ts", EDITED)[0].astHash;

  it("labels an in-place edit merged by fast-forward, including a simpler:complexity signal", async () => {
    const x = await dated();
    expect(editedHash()).not.toBe(hashOf("src/feature.ts", "shorten"));
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    git(x.root, "checkout", "-q", "-b", "work");
    x.put("src/feature.ts", EDITED); // `shorten` occurs as often as before, so `-S` would see nothing
    const tree = x.commitOn(3, "edit shorten in place");
    git(x.root, "checkout", "-q", branch);
    git(x.root, "merge", "-q", "--ff-only", "work");
    insertRun(x.db, {
      id: "run-e", repo: x.name, tree, ts: dayIso(3),
      signals: [
        { type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() },
        { type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: editedHash() },
      ],
    });
    // Rewritten again on the default branch later: both were acted on.
    x.put("src/feature.ts", AGAIN);
    x.commitOn(5, "rewrite shorten");
    expect(await labelOn(x, 4, 18)).toEqual({ "run-e|simpler:complexity|shorten": "acted-on", "run-e|reinvented:exact|shorten": "acted-on" });
    x.db.close();
  });

  it("judges kept at the default branch as of outcomeDays after the run, not at today's tip", async () => {
    const x = await dated();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    insertRun(x.db, { id: "run-w", repo: x.name, tree, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() }] });
    // An unrelated rewrite on day 20, after the run's 14-day window closed on day 17.
    x.put("src/feature.ts", AGAIN);
    x.commitOn(20, "unrelated rewrite later");
    expect(await labelOn(x, 4, 25)).toEqual({ "run-w|simpler:complexity|shorten": "kept" });
    x.db.close();
    // When git can't list the branch's history, the tip is the fallback: the later rewrite reads as acted-on.
    const y = await dated();
    y.put("src/feature.ts", EDITED);
    const t2 = y.commitOn(3, "edit shorten in place");
    insertRun(y.db, { id: "run-w", repo: y.name, tree: t2, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() }] });
    y.put("src/feature.ts", AGAIN);
    y.commitOn(20, "unrelated rewrite later");
    await reconcileShape(y.db, later(y.d, 4), y.loaded, bumpEpoch(y.db));
    await reconcileShape(y.db, { ...later(y.d, 25), git: failingGit("rev-list") }, y.loaded, bumpEpoch(y.db));
    expect(outcomes(y.db)).toEqual({ "run-w|simpler:complexity|shorten": "acted-on" });
    y.db.close();
  });

  it("judges a change merged after its window at today's tip (it wasn't on the branch at the window's end)", async () => {
    const x = await dated();
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    git(x.root, "checkout", "-q", "-b", "late");
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten on a branch");
    git(x.root, "checkout", "-q", branch);
    x.put("src/other.ts", BODY("other"));
    x.commitOn(10, "unrelated main work");
    gitOn(20, x.root, "merge", "-q", "--no-ff", "-m", "late merge", "late");
    insertRun(x.db, { id: "run-l", repo: x.name, tree, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() }] });
    expect(await labelOn(x, 4, 25)).toEqual({ "run-l|simpler:complexity|shorten": "kept" });
    x.db.close();
  });

  it("counts runs that share a staged tree once: the latest run's signals are labeled, the earlier ones n/a", async () => {
    const x = await dated();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    // A hook rejected the first attempt (or the message editor was closed): the retry has the same tree.
    const sig = { type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() };
    insertRun(x.db, { id: "run-1", repo: x.name, tree, ts: dayIso(3), signals: [sig] });
    insertRun(x.db, { id: "run-2", repo: x.name, tree, ts: new Date(Date.parse(dayIso(3)) + 60_000).toISOString(), signals: [sig] });
    expect(await labelOn(x, 4, 18)).toEqual({ "run-1|simpler:complexity|shorten": "n/a", "run-2|simpler:complexity|shorten": "kept" });
    // A later run with the same tree supersedes an earlier one that is already linked but unlabeled.
    insertRun(x.db, { id: "run-0", repo: x.name, tree, ts: dayIso(2), signals: [sig] });
    expect(await reconcileShape(x.db, later(x.d, 19), x.loaded, bumpEpoch(x.db))).toEqual({ linked: 1, labeled: 1 });
    expect(outcomes(x.db)["run-0|simpler:complexity|shorten"]).toBe("n/a");
    x.db.close();
  });

  it("closes an unlinked run once it is outcomeDays old: it never links later and never widens the next tick's git log", async () => {
    const x = await dated();
    insertRun(x.db, { id: "run-old", repo: x.name, tree: "c".repeat(40), ts: dayIso(0), signals: [{ type: "reinvented:exact", at: "src/x.ts:1", name: "x", hash: "a".repeat(64) }] });
    insertRun(x.db, { id: "run-empty", repo: x.name, tree: "d".repeat(40), ts: dayIso(0), signals: [] });
    expect(await reconcileShape(x.db, later(x.d, 15), x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 1 });
    expect(x.db.prepare("SELECT run_id, commit_sha, closed_at IS NOT NULL AS closed FROM shape_runs ORDER BY run_id").all()).toEqual([
      { run_id: "run-empty", commit_sha: null, closed: 1 },
      { run_id: "run-old", commit_sha: null, closed: 1 },
    ]);
    insertRun(x.db, { id: "run-new", repo: x.name, tree: "e".repeat(40), ts: dayIso(20), signals: [] });
    const logs: string[][] = [];
    const spy: GitRunner = { run: async (args, cwd, o) => (args[0] === "log" && logs.push(args), realGitRunner().run(args, cwd, o)) };
    await reconcileShape(x.db, { ...later(x.d, 21), git: spy }, x.loaded, bumpEpoch(x.db));
    expect(logs.map((a) => a.find((v) => v.startsWith("--since=")))).toEqual([`--since=${Math.floor((Date.parse(dayIso(20)) - 86_400_000) / 1000)}`]);
    x.db.close();
  });

  it("picks the as-of state on the default branch's first-parent chain, never a merged side branch's commit", async () => {
    const x = await dated();
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    insertRun(x.db, { id: "run-f", repo: x.name, tree, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() }] });
    // A side branch rewrites it on day 10 (before the day-17 cutoff) but merges only on day 30.
    git(x.root, "checkout", "-q", "-b", "side");
    x.put("src/feature.ts", AGAIN);
    x.commitOn(10, "rewrite on a side branch");
    git(x.root, "checkout", "-q", branch);
    gitOn(30, x.root, "merge", "-q", "--no-ff", "-m", "late merge", "side");
    expect(await labelOn(x, 4, 35)).toEqual({ "run-f|simpler:complexity|shorten": "kept" });
    x.db.close();
  });

  it("counts an amended commit once even when a tick linked the first attempt before the amend", async () => {
    const x = await dated();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    const sig = { type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() };
    insertRun(x.db, { id: "run-1", repo: x.name, tree, ts: dayIso(3), signals: [sig] });
    await reconcileShape(x.db, later(x.d, 3.5), x.loaded, bumpEpoch(x.db)); // links run-1 to the first commit
    gitOn(4, x.root, "commit", "-q", "--amend", "-m", "reworded");
    insertRun(x.db, { id: "run-2", repo: x.name, tree, ts: dayIso(4), signals: [sig] });
    expect(await labelOn(x, 5, 19)).toEqual({ "run-1|simpler:complexity|shorten": "n/a", "run-2|simpler:complexity|shorten": "kept" });
    const [a, b] = x.db.prepare("SELECT commit_sha FROM shape_runs ORDER BY run_id").pluck().all() as string[];
    expect(a).not.toBe(b);
    x.db.close();
  });

  it("labels an in-place edit that is still on the default branch as kept", async () => {
    const x = await dated();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    insertRun(x.db, { id: "run-k", repo: x.name, tree, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: editedHash() }] });
    expect(await labelOn(x, 4, 18)).toEqual({ "run-k|simpler:complexity|shorten": "kept" });
    x.db.close();
  });

  it("reads a bare-path `at` (no \":\") as the whole file name, not one character short", async () => {
    const x = await dated();
    x.put("src/feature.ts", EDITED);
    const tree = x.commitOn(3, "edit shorten in place");
    insertRun(x.db, { id: "run-p", repo: x.name, tree, ts: dayIso(3), signals: [{ type: "simpler:complexity", at: "src/feature.ts", name: "shorten", hash: editedHash() }] });
    expect(await labelOn(x, 4, 18)).toEqual({ "run-p|simpler:complexity|shorten": "kept" });
    x.db.close();
  });

  it("labels a squash merge by the content it landed, then acted-on once the default branch rewrites it", async () => {
    const x = await dated();
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    git(x.root, "checkout", "-q", "-b", "squash-src");
    x.put("src/squashed.ts", BODY("squashed"));
    const tree = x.commitOn(1, "squash source");
    git(x.root, "checkout", "-q", branch);
    git(x.root, "checkout", "-q", "squash-src", "--", "src/squashed.ts");
    x.commitOn(2, "squash merge");
    x.put("src/squashed.ts", BODY("squashed", "out.reverse();"));
    x.commitOn(4, "rewrite squashed");
    insertRun(x.db, {
      id: "run-s", repo: x.name, tree, ts: dayIso(1),
      signals: [{ type: "reinvented:exact", at: "src/squashed.ts:1", name: "squashed", hash: hashOf("src/squashed.ts", "squashed") }],
    });
    expect(await labelOn(x, 2, 16)).toEqual({ "run-s|reinvented:exact|squashed": "acted-on" });
    x.db.close();
  });

  // Merged, then renamed or moved on the default branch: still there, so kept (final review I3).
  it("labels a merged symbol kept after its file is renamed or the symbol moves to another file", async () => {
    for (const move of [
      (x: Awaited<ReturnType<typeof dated>>) => git(x.root, "mv", "src/feature.ts", "src/renamed.ts"),
      (x: Awaited<ReturnType<typeof dated>>) => {
        fs.rmSync(path.join(x.root, "src/feature.ts"));
        // The name also appears in a doc and in a same-name symbol of another shape: neither holds it.
        x.put("docs/notes.md", "shorten is now in src/lib/strings.ts\n");
        x.put("src/other.ts", BODY("shorten", "out.reverse();"));
        x.put("src/lib/strings.ts", BODY("shorten"));
      },
    ]) {
      const x = await dated();
      const tree = git(x.root, "rev-parse", "HEAD^{tree}").trim();
      insertRun(x.db, { id: "run-mv", repo: x.name, tree, signals: [{ type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: hashOf("src/feature.ts", "shorten") }] });
      move(x);
      x.commitOn(3, "move shorten");
      expect(await labelOn(x, 2, 16)).toEqual({ "run-mv|reinvented:exact|shorten": "kept" });
      x.db.close();
    }
  });

  it("labels acted-on when no file at the tip holds the symbol, and leaves it unlabeled when git grep fails", async () => {
    const x = await dated();
    const tree = git(x.root, "rev-parse", "HEAD^{tree}").trim();
    insertRun(x.db, { id: "run-mv", repo: x.name, tree, signals: [{ type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: hashOf("src/feature.ts", "shorten") }] });
    git(x.root, "mv", "src/feature.ts", "src/renamed.ts");
    x.commitOn(3, "rename");
    await reconcileShape(x.db, later(x.d, 2), x.loaded, bumpEpoch(x.db));
    expect(await reconcileShape(x.db, { ...later(x.d, 16), git: failingGit("grep") }, x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 0 });
    expect(outcomes(x.db)).toEqual({ "run-mv|reinvented:exact|shorten": null });
    x.put("src/renamed.ts", BODY("shorten", "out.reverse();"));
    x.commitOn(4, "rewrite");
    expect(await reconcileShape(x.db, later(x.d, 16), x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 1 });
    expect(outcomes(x.db)).toEqual({ "run-mv|reinvented:exact|shorten": "acted-on" });
    x.db.close();
  });

  // Final review I4: a hash from another parser (a TypeScript or INDEXER_VERSION bump) can't be
  // compared with this one's, so that run is left unlabeled rather than mass-labeled acted-on.
  it("leaves a run recorded by another parser unlabeled, and labels the same run from this parser", async () => {
    const x = await dated();
    const tree = git(x.root, "rev-parse", "HEAD^{tree}").trim();
    const signals = [{ type: "reinvented:exact", at: "src/feature.ts:1", name: "shorten", hash: hashOf("src/feature.ts", "shorten") }];
    insertRun(x.db, { id: "run-old", repo: x.name, tree, parser: "parse-ts@0+ts5.0.0", signals });
    insertRun(x.db, { id: "run-now", repo: x.name, tree, signals });
    expect(await labelOn(x, 2, 16)).toEqual({ "run-old|reinvented:exact|shorten": null, "run-now|reinvented:exact|shorten": "kept" });
    x.db.close();
  });

  it("drops a never-merged signal even when the default branch adds a name that contains it", async () => {
    const x = await dated();
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    git(x.root, "checkout", "-q", "-b", "side");
    x.put("src/util/parse.ts", BODY("parse"));
    const tree = x.commitOn(1, "add parse");
    git(x.root, "checkout", "-q", branch);
    x.put("src/util/parse.ts", BODY("parseInt"));
    x.commitOn(2, "add parseInt");
    insertRun(x.db, { id: "run-n", repo: x.name, tree, ts: dayIso(1), signals: [{ type: "reinvented:exact", at: "src/util/parse.ts:1", name: "parse", hash: hashOf("src/util/parse.ts", "parse") }] });
    expect(await labelOn(x, 2, 16)).toEqual({ "run-n|reinvented:exact|parse": "dropped" });
    x.db.close();
  });

  it("drops a dependency that never reached the default branch, and labels one a squash merge landed", async () => {
    const x = await dated();
    const branch = git(x.root, "symbolic-ref", "--short", "HEAD").trim();
    git(x.root, "checkout", "-q", "-b", "deps");
    x.put("package.json", JSON.stringify({ dependencies: { dayjs: "^1", moment: "^2" } }));
    const tree = x.commitOn(1, "add deps");
    git(x.root, "checkout", "-q", branch);
    x.put("package.json", JSON.stringify({ dependencies: { dayjs: "^1" } }));
    x.commitOn(2, "squash merge: dayjs only");
    insertRun(x.db, {
      id: "run-d", repo: x.name, tree, ts: dayIso(1),
      signals: [
        { type: "reinvented:dependency", at: "package.json", name: "dayjs", hash: null },
        { type: "reinvented:dependency", at: "package.json", name: "moment", hash: null },
      ],
    });
    expect(await labelOn(x, 2, 16)).toEqual({ "run-d|reinvented:dependency|dayjs": "kept", "run-d|reinvented:dependency|moment": "dropped" });
    x.db.close();
  });

  it("never links a run to a stash: a stash's index commit carries the staged tree", async () => {
    const x = await dated();
    x.put("src/feature.ts", BODY("shorten", "out.reverse();"));
    git(x.root, "add", "-A");
    const staged = git(x.root, "write-tree").trim();
    git(x.root, "stash", "push", "-q", "-m", "sindri-test-stash");
    expect(git(x.root, "rev-parse", "refs/stash^2^{tree}").trim()).toBe(staged);
    insertRun(x.db, { id: "run-t", repo: x.name, tree: staged, signals: [{ type: "simpler:complexity", at: "src/feature.ts:1", name: "shorten", hash: "a".repeat(64) }] });
    expect(await reconcileShape(x.db, later(x.d, 2), x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 0 });
    expect(await reconcileShape(x.db, later(x.d, 15), x.loaded, bumpEpoch(x.db))).toEqual({ linked: 0, labeled: 1 });
    expect(outcomes(x.db)).toEqual({ "run-t|simpler:complexity|shorten": "dropped" });
    x.db.close();
  });

  it("fetches with ssh in batch mode and no terminal prompt, so the hourly job never waits on one", async () => {
    const { d, db, loaded, root } = await world();
    const bare = path.join(tempDir("sindri-origin-"), "origin.git");
    git(root, "clone", "-q", "--bare", root, bare);
    git(root, "remote", "add", "origin", bare);
    const real = realGitRunner();
    const fetches: { args: string[]; o: Parameters<GitRunner["run"]>[2] }[] = [];
    const spy: GitRunner = {
      run: async (args, cwd, o) => {
        if (args[0] === "fetch") fetches.push({ args, o });
        return real.run(args, cwd, o);
      },
    };
    await reconcileShape(db, { ...later(d, 15), git: spy }, loaded, bumpEpoch(db));
    expect(fetches).toEqual([{ args: ["fetch", "--quiet", "--no-tags", "origin", "refs/heads/main"], o: { foreign: true, env: { GIT_SSH_COMMAND: "ssh -o BatchMode=yes", GIT_TERMINAL_PROMPT: "0" }, timeoutMs: 20_000 } }]);
    expect(FETCH_ENV).toEqual({ GIT_SSH_COMMAND: "ssh -o BatchMode=yes", GIT_TERMINAL_PROMPT: "0" });
    db.close();
  });
});

describe("sindri shape report: outcomes and precision", () => {
  it("reconcile labels outcomes; report then prints per-type labeled counts, precision and progress toward the 3b bar", async () => {
    const { d, db } = await world();
    db.close();
    const rec = await makeShapeCommand(fakeIndexIo())(["reconcile"], later(d, 15));
    expect(rec.stdout).toContain("Reconciled: linked 3 run(s) to commits, labeled 14 signal(s).");
    expect(rec.exitCode).toBe(0);
    const r = await makeShapeCommand(fakeIndexIo())(["report"], later(d, 15));
    expect(r.stdout).not.toContain("Reconciled");
    expect(r.stdout).toContain("Runs: 8 recorded; 1 deferred the embeddings layer.");
    expect(r.stdout).toMatch(/^TYPE\s+SIGNALS\s+LABELED\s+ACTED-ON\s+KEPT\s+PRECISION\s+TOWARD 3b$/m);
    expect(r.stdout).toMatch(/^reinvented:exact\s+9\s+3\s+1\s+2\s+0\.33\s+3\/30 labeled; bar 0\.70$/m);
    expect(r.stdout).toMatch(/^generalize:near-clone\s+1\s+1\s+1\s+0\s+1\.00\s+1\/30 labeled; bar 0\.70$/m);
    expect(r.stdout).toMatch(/^reinvented:dependency\s+3\s+3\s+2\s+1\s+0\.67\s+3\/30 labeled; bar 0\.70$/m);
    expect(r.stdout).toMatch(/^simpler:diff-size\s+1\s+0\s+0\s+0\s+n\/a\s+n\/a \(no flagged symbol\)$/m);
    const json = JSON.parse((await makeShapeCommand(fakeIndexIo())(["report", "--json"], later(d, 15))).stdout);
    expect(json.types.find((t: { key: string }) => t.key === "reinvented:exact")).toMatchObject({ signals: 9, labeled: 3, acted: 1, kept: 2 });
    expect(json.types.find((t: { key: string }) => t.key === "reinvented:exact").precision).toBeCloseTo(1 / 3, 5);
    expect(json.layers.find((l: { key: string }) => l.key === "clones")).toMatchObject({ signals: 18 });
  });

  it("says ready only once 30 signals are labeled and precision is at least 0.7", async () => {
    const d = await approvedIndexDeps(ring0Repo({ "a.ts": "export const a = 1;\n" }));
    const db = openLedger(ledgerPath(stateDir(d)));
    const many = (type: string, acted: number, kept: number): Sig[] => [
      ...Array.from({ length: acted }, (_, i) => ({ type, at: `a.ts:${i}`, name: `a${i}`, hash: null, outcome: "acted-on" })),
      ...Array.from({ length: kept }, (_, i) => ({ type, at: `a.ts:${i}`, name: `k${i}`, hash: null, outcome: "kept" })),
    ];
    insertRun(db, { id: "run-1", repo: ring0Name(d), tree: null, signals: [...many("reinvented:graph", 25, 5), ...many("reinvented:embedding", 10, 20)] });
    db.close();
    const r = await makeShapeCommand(fakeIndexIo())(["report"], d);
    expect(r.stdout).toMatch(/^reinvented:graph\s+30\s+30\s+25\s+5\s+0\.83\s+ready$/m);
    expect(r.stdout).toMatch(/^reinvented:embedding\s+30\s+30\s+10\s+20\s+0\.33\s+30\/30 labeled; bar 0\.70$/m);
  });

  it("report on a machine with no ledger says nothing was recorded, and creates no ledger", async () => {
    const d = makeDeps();
    const r = await makeShapeCommand(fakeIndexIo())(["report"], d);
    expect(r.stdout).toContain("No shape signals recorded yet.");
    expect(fs.existsSync(ledgerPath(stateDir(d)))).toBe(false);
  });

  it("report never migrates, writes or reconciles: a v1 ledger stays v1, and nothing is fetched or labeled", async () => {
    const { d, db, root } = await world();
    db.close();
    const file = ledgerPath(stateDir(d));
    const bytes = fs.readFileSync(file);
    const calls: string[][] = [];
    const spy: GitRunner = { run: async (args, cwd, o) => (calls.push(args), realGitRunner().run(args, cwd, o)) };
    git(root, "remote", "add", "origin", path.join(tempDir("sindri-offline-"), "missing.git"));
    const r = await makeShapeCommand(fakeIndexIo())(["report", "--json"], { ...later(d, 15), git: spy });
    expect(JSON.parse(r.stdout).types.every((t: { labeled: number }) => t.labeled === 0)).toBe(true);
    expect(calls).toEqual([]);
    expect(fs.readFileSync(file).equals(bytes)).toBe(true);
    // A Plan 2 ledger: schema v1, no shape tables.
    const s = makeDeps();
    fs.mkdirSync(stateDir(s), { recursive: true });
    const old = new Database(ledgerPath(stateDir(s)));
    old.pragma("user_version = 1");
    old.close();
    expect((await makeShapeCommand(fakeIndexIo())(["report"], s)).stdout).toContain("No shape signals recorded yet.");
    const check = new Database(ledgerPath(stateDir(s)), { readonly: true });
    expect(schemaVersion(check)).toBe(1);
    check.close();
  });

  it("reconcile runs only with an approved profile, on hosts.active, and outside another run's lock", async () => {
    expect(await makeShapeCommand(fakeIndexIo())(["reconcile", "--json"], makeDeps())).toMatchObject({ exitCode: 1, stdout: expect.stringContaining('"reason": "no approved profile (sindri profile approve)"') });
    const { d, db } = await world();
    const other = await makeShapeCommand(fakeIndexIo())(["reconcile"], { ...later(d, 15), system: { ...d.system, hostname: () => "elsewhere" } });
    expect(other).toMatchObject({ exitCode: 1, stdout: "Not reconciled: this host (elsewhere) is not hosts.active (test-host).\n" });
    const held = acquireTickLock({ dir: stateDir(d), db, sys: d.system, now: d.now });
    const busy = await makeShapeCommand(fakeIndexIo())(["reconcile"], later(d, 15));
    if (held.ok) held.release();
    expect(busy.stdout).toBe("Not reconciled: another run holds the lock.\n");
    expect(outcomes(db)["run-a|reinvented:exact|shorten"]).toBeNull();
    db.close();
  });

  it("reconcile reports a failed step as a note, never as an error", async () => {
    const { d, db } = await world();
    db.close();
    const broken: GitRunner = { run: async () => { throw new Error("git exploded"); } };
    const r = await makeShapeCommand(fakeIndexIo())(["reconcile"], { ...later(d, 15), git: broken });
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("Shape signals not reconciled: git exploded.");
    const odd: GitRunner = { run: async () => { throw "not an Error"; } };
    expect((await makeShapeCommand(fakeIndexIo())(["reconcile"], { ...later(d, 15), git: odd })).stdout).toContain("Shape signals not reconciled: not an Error.");
  });
});

describe("observe reconciles while it records", () => {
  it("links a recorded run to the commit that was then made", async () => {
    const root = ring0Repo({ "src/util/text.ts": BODY("clip") });
    const d = await approvedIndexDeps(root);
    const io = fakeIndexIo();
    await makeIndexCommand(io)(["build", "--repo", ring0Name(d)], d);
    fs.writeFileSync(path.join(root, "src/feature.ts"), BODY("shorten"));
    git(root, "add", "src/feature.ts");
    await makeShapeCommand(io)(["--record", "--staged"], d);
    git(root, "commit", "-qm", "add shorten");
    await runCli(["observe"], d);
    const db = openLedger(ledgerPath(stateDir(d)));
    expect(db.prepare("SELECT commit_sha FROM shape_runs").get()).toEqual({ commit_sha: git(root, "rev-parse", "HEAD").trim() });
    db.close();
  });
});
