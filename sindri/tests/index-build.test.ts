import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SindriError } from "../src/errors.js";
import { buildIndex } from "../src/index/build.js";
import { makeIndexCommand } from "../src/index/commands.js";
import { allSymbols, bandCandidates, depRows, embeddingRows, graphEdges, indexPath, layers, meta, openIndex, openIndexReadOnly, symbolsByAstHash } from "../src/index/db.js";
import { mirrorPath, refreshMirror } from "../src/index/mirror.js";
import { runCli } from "../src/main.js";
import { approvedIndexDeps, BODY, fakeIndexIo, profileFor, ring0Name, ring0Repo } from "./index-fixtures.js";
import { git, gitRepo, makeDeps, tempDir } from "./helpers.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { stateDir } from "../src/deps.js";
import Database from "better-sqlite3";

afterEach(() => vi.restoreAllMocks());

const FILES = {
  "src/a.ts": "export function add(a: number, b: number): number {\n  return a + b;\n}\n",
  "src/b.ts": "export function sum(x: number, y: number): number {\n  return x + y;\n}\n",
  "src/util/strings.ts": "export const trim = (s: string) => s.trim();\n",
  "package.json": JSON.stringify({ dependencies: { dayjs: "^1" } }),
  ".env.local.ts": "export const k = 1;\n",
};

const none = { embedder: null, graph: null };

describe("buildIndex", () => {
  it("indexes structure, clones and deps, and reports disabled layers", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const report = await buildIndex(d, profileFor(root), "r", { full: false }, none);
    expect(report.files).toEqual({ indexed: 4, changed: 3, removed: 0, skipped: 1 });
    expect(report.symbols).toBe(3);
    expect(report.quick).toBe(false);
    expect(report.layers.structure.status).toBe("ok");
    expect(report.layers.embeddings).toEqual({ status: "disabled", detail: "no embedder configured" });
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const addHash = (db.prepare("SELECT ast_hash FROM symbols WHERE name = 'add'").get() as { ast_hash: string }).ast_hash;
    const add = symbolsByAstHash(db, addHash);
    expect(add.map((s) => s.name).sort()).toEqual(["add", "sum"]);
    expect(add.find((s) => s.name === "add")?.utility).toBe(false);
    const trim = db.prepare("SELECT utility FROM symbols WHERE name = 'trim'").get() as { utility: number };
    expect(trim.utility).toBe(1);
    expect(depRows(db)).toEqual([{ manifest: "package.json", name: "dayjs", version: "^1", kind: "prod", tags: ["date"] }]);
    expect(bandCandidates(db, [])).toEqual([]);
    expect(meta(db).commit).toMatch(/^[0-9a-f]{40}$/);
    expect(layers(db).map((l) => l.layer).sort()).toEqual(["clones", "deps", "embeddings", "graph", "structure"]);
    db.close();
  });

  it("rebuilds only changed files, drops removed ones, and --full rebuilds everything", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function add(a: number): number {\n  return a;\n}\n");
    fs.rmSync(path.join(root, "src/b.ts"));
    git(root, "add", "-A");
    const second = await buildIndex(d, p, "r", { full: false }, none);
    expect(second.files).toMatchObject({ changed: 1, removed: 1 });
    const third = await buildIndex(d, p, "r", { full: true }, none);
    expect(third.files).toMatchObject({ changed: 2, removed: 0 });
  });

  it("a quick build skips the network layers: pending on a fresh index, untouched after", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    const quick = await buildIndex(d, p, "r", { full: false, quick: true }, none);
    expect(quick.quick).toBe(true);
    expect(quick.layers.structure.status).toBe("ok");
    expect(quick.layers.embeddings).toEqual({ status: "pending", detail: "not built yet (sindri index build)" });
    expect(quick.layers.graph.status).toBe("pending");
    const full = await buildIndex(d, p, "r", { full: false }, none);
    expect(full.layers.embeddings.status).toBe("disabled");
    const quickAgain = await buildIndex(d, p, "r", { full: false, quick: true }, none);
    expect(quickAgain.layers.embeddings.status).toBe("disabled");
  });

  it("keeps the previous index when a build fails halfway (Review Focus 2)", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const p = profileFor(root);
    await buildIndex(d, p, "r", { full: false }, none);
    const before = fs.readFileSync(indexPath(d, "r"));
    fs.writeFileSync(path.join(root, "src/a.ts"), "export function changed() { return 1; }\n");
    const real = fs.renameSync;
    vi.spyOn(fs, "renameSync").mockImplementation((from, to) => {
      if (String(to) === indexPath(d, "r")) throw new Error("disk full");
      real(from, to);
    });
    await expect(buildIndex(d, p, "r", { full: false }, none)).rejects.toThrow("disk full");
    expect(fs.readFileSync(indexPath(d, "r"))).toEqual(before);
    expect(fs.readdirSync(path.dirname(indexPath(d, "r"))).filter((n) => n.includes(".tmp-"))).toEqual([]);
  });

  it("closes the temp copy and removes it when a build step throws before the swap", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const real = d.git;
    const exploding = { run: async (args: string[], cwd: string) => { if (args[0] === "rev-parse") throw new Error("git exploded"); return real.run(args, cwd); } };
    await expect(buildIndex({ ...d, git: exploding }, profileFor(root), "r", { full: false }, none)).rejects.toThrow("git exploded");
    expect(fs.readdirSync(path.dirname(indexPath(d, "r"))).filter((n) => n.includes(".tmp-"))).toEqual([]);
  });

  it("sweeps temp copies a killed build left behind, and keeps fresh ones", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    const dir = path.dirname(indexPath(d, "r"));
    fs.mkdirSync(dir, { recursive: true });
    const old = path.join(dir, "r.db.tmp-old");
    const fresh = path.join(dir, "r.db.tmp-fresh");
    const otherRepo = path.join(dir, "x.db.tmp-old");
    for (const f of [old, fresh, otherRepo]) fs.writeFileSync(f, "x");
    fs.utimesSync(old, new Date("2026-10-08T09:00:00Z"), new Date("2026-10-08T09:00:00Z"));
    fs.utimesSync(fresh, new Date("2026-10-08T11:45:00Z"), new Date("2026-10-08T11:45:00Z"));
    fs.utimesSync(otherRepo, new Date("2026-10-08T09:00:00Z"), new Date("2026-10-08T09:00:00Z"));
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    expect([fs.existsSync(old), fs.existsSync(fresh), fs.existsSync(otherRepo)]).toEqual([false, true, true]);
  });

  it("scrubs secret-shaped strings out of stored symbol bodies", async () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const root = gitRepo({ "src/k.ts": `export function key() { return "${secret}"; }\n` });
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    const row = db?.prepare("SELECT body FROM symbols WHERE name = 'key'").get() as { body: string };
    db?.close();
    expect(row.body).toContain("export function key()");
    expect(row.body).not.toContain(secret);
  });

  it("re-parses every file when index.utilityGlobs changes", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const again = await buildIndex(d, profileFor(root, { utility: [] }), "r", { full: false }, none);
    expect(again.files.changed).toBe(3);
    const db = openIndexReadOnly(indexPath(d, "r"));
    expect(db?.prepare("SELECT utility FROM symbols WHERE name = 'trim'").get()).toEqual({ utility: 0 });
    db?.close();
  });

  it("reads symbols, embeddings and graph edges back (empty for a fresh build)", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const all = allSymbols(db);
    expect(all.map((s) => s.name).sort()).toEqual(["add", "sum", "trim"]);
    expect(all[0].minhash).toBeInstanceOf(Uint32Array);
    expect(all.find((s) => s.name === "trim")?.exported).toBe(true);
    expect(bandCandidates(db, [])).toEqual([]);
    expect(embeddingRows(db, "m")).toEqual([]);
    expect(graphEdges(db)).toEqual([]);
    const keys = (db.prepare("SELECT key FROM bands WHERE symbol_id = ?").all(all[0].id) as { key: string }[]).map((r) => r.key);
    expect(bandCandidates(db, keys)).toContain(all[0].id);
    db.close();
    const rw = openIndex(indexPath(d, "r"));
    rw.prepare("INSERT INTO embeddings (symbol_id, model, vector) VALUES (?, 'm', ?)").run(all[0].id, Buffer.from([1, 2]));
    rw.prepare("INSERT INTO graph_edges (src, dst, relation, confidence) VALUES ('a', 'b', 'calls', 'EXTRACTED')").run();
    expect(embeddingRows(rw, "m")).toEqual([{ symbolId: all[0].id, vector: Buffer.from([1, 2]) }]);
    expect(embeddingRows(rw, "other")).toEqual([]);
    expect(graphEdges(rw)).toEqual([{ src: "a", dst: "b", relation: "calls", confidence: "EXTRACTED" }]);
    rw.close();
  });

  it("reports no commit for a repo without commits", async () => {
    const root = tempDir("sindri-empty-");
    git(root, "init", "-q", "-b", "main");
    const report = await buildIndex(makeDeps(), profileFor(root), "r", { full: false }, none);
    expect(report.commit).toBeNull();
    expect(report.symbols).toBe(0);
  });

  it("does not band symbols with fewer tokens than one shingle (no tiny-symbol clones)", async () => {
    const root = gitRepo({ "src/t.ts": "function p() {}\nexport function q() {}\n" + BODY("big") });
    const d = makeDeps();
    await buildIndex(d, profileFor(root), "r", { full: false }, none);
    const db = openIndexReadOnly(indexPath(d, "r"));
    if (db === null) throw new Error("no index");
    const rows = db.prepare("SELECT name, token_count, (SELECT COUNT(*) FROM bands WHERE symbol_id = symbols.id) AS n FROM symbols ORDER BY name").all() as { name: string; token_count: number; n: number }[];
    db.close();
    const tiny = rows.filter((r) => r.name === "p" || r.name === "q");
    expect(tiny.every((r) => r.token_count < 5 && r.n === 0)).toBe(true);
    expect(rows.find((r) => r.name === "big")?.n).toBeGreaterThan(0);
  });

  it("refuses an unknown repo", async () => {
    const root = gitRepo(FILES);
    await expect(buildIndex(makeDeps(), profileFor(root), "nope", { full: false }, none)).rejects.toThrow(/SND-PROFILE-004|no repo named nope/);
  });
});

describe("mirror", () => {
  it("creates a bare mirror, refreshes it, and reports a repo git can't clone", async () => {
    const root = gitRepo(FILES);
    const d = makeDeps();
    expect(await refreshMirror(d, "r", root)).toBe(mirrorPath(d, "r"));
    expect(fs.existsSync(path.join(mirrorPath(d, "r"), "HEAD"))).toBe(true);
    fs.writeFileSync(path.join(root, "src/c.ts"), "export const c = 1;\n");
    git(root, "add", "-A");
    git(root, "commit", "-qm", "second");
    await refreshMirror(d, "r", root);
    expect(git(root, "--git-dir", mirrorPath(d, "r"), "log", "--format=%s")).toContain("second");
    const err = await refreshMirror(d, "x", tempDir()).catch((e: unknown) => e);
    expect((err as SindriError).code).toBe("SND-INDEX-002");
  });
});

describe("sindri index build | status", () => {
  const idx = makeIndexCommand(fakeIndexIo());

  it("builds every repo in the approved profile, mirrors it, and reports status", async () => {
    const root = ring0Repo(FILES);
    const logs: string[] = [];
    const d = { ...(await approvedIndexDeps(root)), log: (l: string) => logs.push(l) };
    const build = await idx(["build"], d);
    expect(build.exitCode).toBe(0);
    expect(build.stdout).toMatch(/: 4 files \(3 changed, 0 removed, 1 skipped\), 3 symbols; structure ok, clones ok, deps ok, embeddings disabled, graph disabled/);
    expect(logs).toEqual([expect.stringContaining("; this takes the heavy-job lock")]);
    expect(fs.existsSync(path.join(mirrorPath(d, ring0Name(d)), "HEAD"))).toBe(true);
    const status = await idx(["status"], d);
    expect(status.exitCode).toBe(0);
    expect(status.stdout).toMatch(/built 0 min ago at [0-9a-f]{12}; structure ok/);
    const json = JSON.parse((await idx(["status", "--json"], d)).stdout);
    expect(json[0]).toMatchObject({ missing: false, stale: false, layers: { structure: { status: "ok" } } });
  });

  it("mirrors on full builds only", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root);
    await idx(["build", "--quick"], d);
    expect(fs.existsSync(mirrorPath(d, ring0Name(d)))).toBe(false);
    await idx(["build"], d);
    expect(fs.existsSync(mirrorPath(d, ring0Name(d)))).toBe(true);
  });

  it("lists every repo, including one with no index, and exits 1 (nothing aborts)", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root, { extraRepos: ["ghost"] });
    const name = ring0Name(d);
    await idx(["build", "--repo", name], d);
    const r = await idx(["status"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain(`${name}: built `);
    expect(r.stdout).toContain("ghost: no index (sindri index build --repo ghost)");
    const missing = JSON.parse((await idx(["status", "--json", "--repo", "ghost"], d)).stdout);
    expect(missing).toEqual([{ repo: "ghost", missing: true, commit: "", builtAt: null, ageMs: null, stale: true, layers: {} }]);
  });

  it("says never built for an empty index, and shows the reason for a layer that is not ok", async () => {
    const root = ring0Repo(FILES);
    const d = await approvedIndexDeps(root);
    openIndex(indexPath(d, ring0Name(d))).close();
    const r = await idx(["status"], d);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain("stale (never built); structure pending (not built yet), clones pending (not built yet)");
    expect(r.stdout).toContain("  fix: sindri index build --repo");
    expect(r.stdout).not.toContain("NaN");
  });

  it("marks a stale index (older than index.maxAgeHours) with the word stale and a fix line", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    await idx(["build"], d);
    const later = { ...d, now: () => new Date(d.now().getTime() + 48 * 3_600_000) };
    const r = await idx(["status"], later);
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toMatch(/stale \(built 48 h ago at [0-9a-f]{12}\)/);
    expect(r.stdout).toContain(`  fix: sindri index build --repo ${ring0Name(d)}`);
  });

  it("says no approved profile when the ledger exists but nothing was approved", async () => {
    const d = makeDeps();
    openLedger(ledgerPath(stateDir(d))).close();
    expect((await idx(["status"], d)).stderr).toContain("SND-PROFILE-012");
  });

  it("never creates or migrates the ledger (status reads it read-only)", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    const file = ledgerPath(stateDir(d));
    const raw = new Database(file);
    raw.pragma("wal_checkpoint(TRUNCATE)");
    raw.pragma("user_version = 1");
    raw.close();
    const before = fs.readFileSync(file);
    expect((await idx(["status"], d)).stderr).not.toContain("SND-");
    expect(fs.readFileSync(file)).toEqual(before);
    const check = new Database(file, { readonly: true });
    expect(check.pragma("user_version", { simple: true })).toBe(1);
    check.close();
    const bare = makeDeps();
    expect((await idx(["status"], bare)).stderr).toContain("SND-PROFILE-012");
    expect(fs.existsSync(ledgerPath(stateDir(bare)))).toBe(false);
  });

  it("says no approved profile, an unknown subcommand, an unknown repo, and shows usage through the registry", async () => {
    const d = await approvedIndexDeps(ring0Repo(FILES));
    expect((await idx(["build"], makeDeps())).stderr).toContain("SND-PROFILE-012");
    expect((await idx(["frob"], d)).stderr).toContain("SND-CLI-002");
    expect((await idx([], d)).stderr).toContain("unknown index subcommand: (none)");
    expect((await idx(["build", "--repo", "zzz"], d)).stderr).toContain("SND-PROFILE-004");
    const help = await runCli(["index", "--help"], makeDeps());
    expect(help.stdout).toContain("sindri index build [--repo NAME] [--quick] [--full] [--json]");
    expect(help.stdout).toContain("sindri index setup [--dry-run] [--json]");
  });
});
