import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { stateDir } from "../src/deps.js";
import { runCli } from "../src/main.js";
import { ledgerFileVersion, ledgerPath, openLedger } from "../src/ledger/db.js";
import { acquireTickLock } from "../src/lock/lock.js";
import { extractSection, makeScopeCommand, recordRun, writeOut } from "../src/scope/commands.js";
import type { ScopeMap } from "../src/scope/map.js";
import type { GraphqlFetch } from "../src/scope/sources/linear.js";
import { ModelJobError } from "../src/scope/model.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "../src/scrub/scrub.js";
import { fakeGit, fakeSystem, git, gitRepo, makeDeps, tempDir } from "./helpers.js";
import { ring0Name } from "./index-fixtures.js";
import { approvedScopeDeps, scriptedIo } from "./scope-fixtures.js";

const MAP: ScopeMap = {
  subject: "Shift times",
  surfaces: [{ id: "S1", kind: "ui", title: "Editor", detail: "", citations: ["R1"] }],
  implications: [],
  workstreams: [{ id: "W1", title: "Editor", surfaces: ["S1"], dependsOn: [], acceptance: ["edits"] }],
  questions: [{ question: "Overnight?", options: [], citations: ["R1"] }],
};
const NONE = { missing: [], workstream: "" };
const BRIEF = "# Shift times\nAdd shift times to the scheduling editor.\n";

function briefFile(text = BRIEF, name = "brief.md"): string {
  const f = path.join(tempDir(), name);
  fs.writeFileSync(f, text);
  return f;
}
const rows = (d: Parameters<typeof stateDir>[0], sql: string): unknown[] => {
  const db = openLedger(ledgerPath(stateDir(d)));
  try {
    return db.prepare(sql).all();
  } finally {
    db.close();
  }
};

describe("extractSection", () => {
  it("returns a numbered level-2 section", () => {
    const md = "# T\n## 12. Portability\nx\n## 13. Rollout\nsteps\n### 13.1 Sub\nmore\n## 14. Testing\n";
    expect(extractSection(md, "13")).toBe("## 13. Rollout\nsteps\n### 13.1 Sub\nmore");
    expect(extractSection(md, "99")).toBeNull();
    expect(extractSection("# T\n## 7 Notes\nz\n", "7")).toBe("## 7 Notes\nz");
  });
});

describe("sindri scope <file>", () => {
  it("writes a complete scope map, shows the next action, and records the run and its model calls", async () => {
    const d = await approvedScopeDeps();
    const out = tempDir();
    const io = scriptedIo([MAP, NONE]);
    const r = await makeScopeCommand(io)([briefFile(), "--out", out], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toMatch(/^Scope map for "Shift times": complete, 1 surfaces, 1 workstreams, 1 open questions \(2 rounds, 110 tokens\)\./);
    expect(r.stdout).toContain("Sources: code 0.");
    expect(r.stdout).toContain('1 open questions need answers before issues are created (see "Open questions" in scope-shift-times-2026-10-08.md).');
    expect(r.stderr).toContain("Note: code: no matching records");
    expect(io.lines).toEqual(["gathering…", "drafting (round 1)…", "challenging (round 1)…"]);
    const md = fs.readdirSync(out).find((f) => f.endsWith(".md")) as string;
    expect(md).toBe("scope-shift-times-2026-10-08.md");
    expect(fs.readFileSync(path.join(out, md), "utf8")).toContain("# Scope map: Shift times");
    const saved = JSON.parse(fs.readFileSync(path.join(out, md.replace(".md", ".json")), "utf8"));
    expect(saved).toMatchObject({ status: "complete", subject: "brief.md", counts: { code: 0 } });
    expect(rows(d, "SELECT role, model, input_tokens AS i, output_tokens AS o FROM model_calls ORDER BY seq")).toEqual([
      { role: "draft", model: "sonnet", i: 50, o: 5 }, { role: "challenge", model: "opus", i: 50, o: 5 },
    ]);
    const again = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", out], d);
    expect(again.stdout).toContain("scope-shift-times-2026-10-08-2.md");
    expect(rows(d, "SELECT mode, status, surfaces, subject FROM scope_runs")).toEqual([
      { mode: "scope", status: "complete", surfaces: 1, subject: "brief.md" }, { mode: "scope", status: "complete", surfaces: 1, subject: "brief.md" },
    ]);
    expect(rows(d, "SELECT COUNT(*) AS n FROM model_calls")).toEqual([{ n: 4 }]);
  });

  it("says when there are no open questions, names a file whose title has no letters, and gives --json a summary", async () => {
    const d = await approvedScopeDeps();
    const out = tempDir();
    const none = { ...MAP, questions: [] };
    const r = await makeScopeCommand(scriptedIo([none, NONE]))([briefFile("# !!!\nshift times editor\n"), "--out", out], d);
    expect(r.stdout).toContain("No open questions.");
    expect(fs.readdirSync(out)).toContain("scope-scope-2026-10-08.md");
    const j = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", tempDir(), "--json"], d);
    expect(JSON.parse(j.stdout)).toMatchObject({ status: "complete", surfaces: 1, workstreams: 1, questions: 1, counts: { code: 0 }, reasons: [], recorded: true });
    expect(j.stderr).toBe("");
  });

  it("scopes one section of a long doc; an incomplete run exits 1, explains itself on stderr and in the file", async () => {
    const d = await approvedScopeDeps();
    const spec = briefFile("# Spec\n## 12. Other\nno\n## 13. Rollout\nshift times rollout\n", "spec.md");
    const bad = { ...MAP, workstreams: [] };
    const io = scriptedIo([bad, bad, bad]);
    const out = tempDir();
    const r = await makeScopeCommand(io)([spec, "--section", "13", "--out", out], d);
    expect(io.inputs[0]).toContain("shift times rollout");
    expect(io.inputs[0]).not.toContain("## 12. Other");
    expect(r.exitCode).toBe(1);
    expect(r.stdout).toContain('Scope map for "13. Rollout": incomplete, 0 surfaces, 0 workstreams, 0 open questions (3 rounds, 165 tokens).');
    expect(r.stdout).toContain("Rerun after raising scope.maxRounds or scope.maxTokensPerRun");
    expect(r.stderr).toContain("Why incomplete: surface S1 is in no workstream");
    const md = fs.readFileSync(path.join(out, "scope-13-rollout-2026-10-08.md"), "utf8");
    expect(md).toContain("No scope map passed the checks.");
    expect(md).toContain("- surface S1 is in no workstream");
    expect(rows(d, "SELECT status, surfaces FROM scope_runs")).toEqual([{ status: "incomplete", surfaces: 0 }]);
  });

  it("refuses a missing file, section, output dir, Linear config, source name, unknown flag or approval", async () => {
    const d = await approvedScopeDeps();
    const cmd = makeScopeCommand(scriptedIo([]));
    const f = briefFile("# B\n");
    expect((await cmd(["/no/such.md", "--out", tempDir()], d)).stderr).toContain("SND-SCOPE-020");
    expect((await cmd([f, "--section", "7", "--out", tempDir()], d)).stderr).toContain("SND-SCOPE-022");
    expect((await cmd([f], d)).stderr).toContain("SND-SCOPE-021");
    expect((await cmd(["linear:abc", "--out", tempDir()], d)).stderr).toContain("SND-SCOPE-024");
    expect((await cmd(["https://linear.app/acme/project/new-abc123", "--out", tempDir()], d)).stderr).toContain("SND-SCOPE-024");
    expect((await cmd(["linear:abc", "--section", "3", "--out", tempDir()], d)).stderr).toContain("--section applies to brief files");
    expect((await cmd([], d)).stderr).toContain("SND-CLI-002");
    expect((await cmd([f, "--bogus"], d)).stderr).toContain("SND-CLI-002");
    expect((await cmd([f, "--sources", "bogus", "--out", tempDir()], d)).stderr).toContain("unknown source bogus");
    expect((await cmd([f, "--out", tempDir()], makeDeps())).stderr).toContain("SND-PROFILE-012");
  });
});

describe("the public-repo guard (Review Focus 7)", () => {
  it("refuses to write into a git worktree unless --sources is only file and/or code and the subject is a file", async () => {
    const d = await approvedScopeDeps();
    const f = briefFile();
    const cmd = makeScopeCommand(scriptedIo([]));
    const inside = path.join(d.cwd, "scopes");
    for (const extra of [[], ["--sources", "notes"], ["--sources", "file,code,linear"]]) {
      expect((await cmd([f, "--out", inside, ...extra], d)).stderr).toContain("SND-SCOPE-025");
    }
    expect((await cmd(["linear:abc", "--out", inside, "--sources", "file"], d)).stderr).toContain("SND-SCOPE-025");
    expect(fs.existsSync(inside)).toBe(false);
  });

  it("allows --sources file,code, resolves a relative --out against the cwd, and writes 0600 files in 0700 directories", async () => {
    const d = await approvedScopeDeps();
    const r = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", "rel-out/nested", "--sources", "file,code"], d);
    expect(r.exitCode).toBe(0);
    const dir = path.join(d.cwd, "rel-out", "nested");
    expect(fs.statSync(dir).mode & 0o777).toBe(0o700);
    const files = fs.readdirSync(dir);
    expect(files).toHaveLength(2);
    for (const name of files) expect(fs.statSync(path.join(dir, name)).mode & 0o777).toBe(0o600);
  });
});

describe("sources, dry runs and the notes dir", () => {
  it("--dry-run lists the sources it would read with counts, calls no model and writes nothing", async () => {
    const notes = tempDir();
    fs.writeFileSync(path.join(notes, "n.md"), "shift times scheduling notes");
    const tdir = tempDir();
    fs.writeFileSync(path.join(tdir, "s.jsonl"), `${JSON.stringify({ type: "user", timestamp: "2026-01-01T00:00:00Z", message: { role: "user", content: "the shift times editor needs work" } })}\n`);
    const d = await approvedScopeDeps(`  notesDir: ${notes}\n  transcripts:\n    enabled: true\n    dir: ${tdir}\n`);
    const io = scriptedIo([]);
    const cmd = makeScopeCommand(io);
    const f = briefFile();
    const all = await cmd([f, "--dry-run"], d);
    expect(all.exitCode).toBe(0);
    expect(all.stdout).toContain("Sources it would read: notes 1, transcripts 1, code 0.");
    expect(all.stdout).toContain("Models: draft sonnet, challenge opus; up to 3 rounds each; budget 600000 tokens.");
    expect((await cmd([f, "--dry-run", "--sources", "code"], d)).stdout).toContain("Sources it would read: code 0.");
    expect((await cmd([f, "--dry-run", "--sources", ""], d)).stdout).toContain("Sources it would read: none.");
    expect(JSON.parse((await cmd([f, "--dry-run", "--json"], d)).stdout)).toMatchObject({ dryRun: true, title: "Shift times", counts: { notes: 1, transcripts: 1, code: 0 } });
    expect(io.inputs).toEqual([]);
    expect(rows(d, "SELECT COUNT(*) AS n FROM scope_runs")).toEqual([{ n: 0 }]);
  });

  it("writes to sources.notesDir by default, and never reads its own generated maps back as notes", async () => {
    const notes = tempDir();
    const d = await approvedScopeDeps(`  notesDir: ${notes}\n`);
    const r = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--sources", "code"], d);
    expect(r.exitCode).toBe(0);
    expect(fs.readdirSync(notes).sort()).toEqual(["scope-shift-times-2026-10-08.json", "scope-shift-times-2026-10-08.md"]);
    const dry = await makeScopeCommand(scriptedIo([]))([briefFile(), "--dry-run", "--sources", "notes"], d);
    expect(dry.stdout).toContain("Sources it would read: notes 0.");
  });
});

describe("sindri scope <linear project>", () => {
  const fetchFor = (): GraphqlFetch => async (_u, init) => {
    const q = JSON.parse(init.body) as { query: string };
    if (q.query.includes("projects(")) return { ok: true, status: 200, json: async () => ({ data: { projects: { nodes: [{ id: "p1", name: "Shift times", description: "brief text about shift times", createdAt: "2026-01-01T00:00:00Z", url: "u" }] } } }) };
    return { ok: true, status: 200, json: async () => ({ data: { project: { issues: { pageInfo: { hasNextPage: false, endCursor: null }, nodes: [{ identifier: "ABC-1", title: "Shift times editor", description: "d", createdAt: "2026-01-02T00:00:00Z", url: "u", creator: null, comments: { nodes: [] } }] } } } }) };
  };

  it("uses the project as the brief and its issues as a source; --sources can leave the issues out", async () => {
    const d = await approvedScopeDeps("  linear:\n    token: env:LINEAR_TOKEN\n");
    const withToken = { ...d, env: { ...d.env, LINEAR_TOKEN: "tok" } };
    const io = scriptedIo([MAP, NONE], fetchFor());
    const r = await makeScopeCommand(io)(["linear:abc", "--out", tempDir()], withToken);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Sources: linear 1, code 0.");
    expect(io.inputs[0]).toContain('ref="linear:ABC-1"');
    expect(rows(d, "SELECT subject FROM scope_runs")).toEqual([{ subject: "linear:abc" }]);
    const dry = await makeScopeCommand(scriptedIo([], fetchFor()))(["linear:abc", "--dry-run"], withToken);
    expect(dry.stdout).toContain("Sources it would read: linear 1, code 0.");
    const codeOnly = await makeScopeCommand(scriptedIo([], fetchFor()))(["linear:abc", "--dry-run", "--sources", "code"], withToken);
    expect(codeOnly.stdout).toContain("Sources it would read: code 0.");
  });
});

describe("the ledger", () => {
  it("says so, loudly, when another run holds the lock and the row can't be written", async () => {
    const d = await approvedScopeDeps();
    const db = openLedger(ledgerPath(stateDir(d)));
    const held = acquireTickLock({ dir: stateDir(d), db, sys: fakeSystem({ pid: 5555 }), now: d.now });
    expect(held.ok).toBe(true);
    const r = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", tempDir()], d);
    if (held.ok) held.release();
    db.close();
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Not recorded in the ledger: another run holds the lock.");
    expect(rows(d, "SELECT COUNT(*) AS n FROM scope_runs")).toEqual([{ n: 0 }]);
  });

  it("sindri scope runs lists the latest runs, newest first, as text and JSON", async () => {
    expect((await makeScopeCommand(scriptedIo([]))(["runs"], makeDeps())).stdout).toBe("No scope runs recorded.\n");
    const d = await approvedScopeDeps();
    const io = scriptedIo([MAP, NONE, MAP, NONE]);
    const out = tempDir();
    await makeScopeCommand(io)([briefFile(), "--out", out], d);
    await makeScopeCommand(io)([briefFile(), "--out", out], d);
    expect(recordRun(d, {
      runId: "r-backtest", subject: "linear:abc", mode: "backtest", status: "complete", rounds: 2, surfaces: 1, recall: 0.5, precision: 1,
      baselineRecall: 0, baselinePrecision: 0, leaky: true, tokens: 660, outPath: "/x/backtest-p-2026-10-09.md",
    }, [])).toBe(true);
    const text = (await makeScopeCommand(io)(["runs"], d)).stdout.trim().split("\n");
    expect(text).toEqual([
      "2026-10-08T12:00:00.000Z backtest complete surfaces=1 recall=0.50 precision=1.00 leaky backtest-p-2026-10-09.md",
      "2026-10-08T12:00:00.000Z scope complete surfaces=1 recall=n/a precision=n/a scope-shift-times-2026-10-08-2.md",
      "2026-10-08T12:00:00.000Z scope complete surfaces=1 recall=n/a precision=n/a scope-shift-times-2026-10-08.md",
    ]);
    const json = JSON.parse((await makeScopeCommand(io)(["runs", "--json"], d)).stdout) as Record<string, unknown>[];
    expect(json[0]).toMatchObject({ mode: "backtest", recall: 0.5, precision: 1, baselineRecall: 0, leaky: true, file: "backtest-p-2026-10-09.md" });
    expect(json[1]).toMatchObject({ mode: "scope", recall: null, leaky: false });
  });
});

describe("read-only paths never create or migrate the ledger", () => {
  it("scope runs and --dry-run with no ledger create nothing", async () => {
    const d = makeDeps();
    const cmd = makeScopeCommand(scriptedIo([]));
    expect(JSON.parse((await cmd(["runs", "--json"], d)).stdout)).toEqual([]);
    expect((await cmd([briefFile(), "--dry-run"], d)).stderr).toContain("SND-PROFILE-012");
    expect(fs.existsSync(stateDir(d))).toBe(false);
  });

  it("--dry-run and scope runs read approval from a v2 ledger and leave it byte-identical, at v2, with no backup (final review M1)", async () => {
    const d = await approvedScopeDeps();
    const file = ledgerPath(stateDir(d));
    const db = openLedger(file);
    db.exec("DROP TABLE model_calls; DROP TABLE scope_runs;");
    db.pragma("user_version = 2");
    db.close();
    const before = fs.readFileSync(file);
    const listing = fs.readdirSync(stateDir(d)).sort();
    const cmd = makeScopeCommand(scriptedIo([]));
    const dry = await cmd([briefFile(), "--dry-run"], d);
    expect(dry.stderr).toBe("");
    expect(dry.exitCode).toBe(0);
    expect(dry.stdout).toContain('Dry run for "Shift times".');
    expect((await cmd(["runs"], d)).stdout).toBe("No scope runs recorded.\n");
    expect(fs.readFileSync(file).equals(before)).toBe(true);
    expect(fs.readdirSync(stateDir(d)).sort()).toEqual(listing);
    expect(ledgerFileVersion(file)).toBe(2);
  });
});

describe("the profile's scrub patterns", () => {
  it("reach every source, the model's egress, the written map and JSON, the ledger and stdout", async () => {
    const notes = tempDir();
    fs.writeFileSync(path.join(notes, "n.md"), "shift times editor QQX-222222");
    const d = await approvedScopeDeps(`  notesDir: ${notes}\n`, 'scrub:\n  extraPatterns:\n    - kind: house-id\n      regex: "QQX-[0-9]{6}"\n');
    let egress: Scrubber | undefined;
    const io = scriptedIo([{ ...MAP, surfaces: [{ ...MAP.surfaces[0], title: "Editor QQX-333333" }], questions: [{ question: "QQX-666666?", options: [], citations: ["R1"] }] }, NONE]);
    const out = tempDir();
    const cmd = makeScopeCommand({ ...io, runner: (l, s) => { egress = s; return io.runner(l, s); } });
    const r = await cmd([briefFile(`${BRIEF}QQX-111111 shift times\n`, "QQX-444444.md"), "--out", out], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Sources: notes 1, code 0.");
    expect(egress?.scrub("QQX-555555").text).toBe("[REDACTED:house-id]");
    expect(io.inputs.join("\n")).not.toContain("QQX");
    expect(fs.readdirSync(out)).toHaveLength(2);
    for (const f of fs.readdirSync(out)) expect(fs.readFileSync(path.join(out, f), "utf8")).not.toContain("QQX");
    expect(JSON.stringify([rows(d, "SELECT * FROM scope_runs"), rows(d, "SELECT * FROM model_calls")])).not.toContain("QQX");
    expect(r.stdout + r.stderr).not.toContain("QQX");
  });
});

describe("writeOut", () => {
  it("scrubs the map text and the JSON with the given scrubber and writes only inside the directory", () => {
    const dir = path.join(tempDir(), "maps");
    const scrubber = makeScrubber(compileExtraPatterns([{ kind: "house-id", regex: "QQX-[0-9]{6}" }]));
    const file = writeOut(dir, "scope-x", "# QQX-123456\n", { title: "QQX-654321" }, scrubber);
    expect(path.dirname(file)).toBe(dir);
    expect(fs.readdirSync(path.dirname(dir))).toEqual(["maps"]);
    expect(fs.readFileSync(file, "utf8")).toBe("# [REDACTED:house-id]\n");
    expect(JSON.parse(fs.readFileSync(file.replace(/\.md$/, ".json"), "utf8"))).toEqual({ title: "[REDACTED:house-id]" });
  });
});

describe("the code source inside a worktree", () => {
  it("reads only the profile repo that holds --out, and refuses when no profile repo does", async () => {
    const other = gitRepo({ "src/other.ts": "export function shiftTimesEditorOther() {\n  return 2;\n}\n" });
    const d = await approvedScopeDeps("", "", [{ name: "other", path: other }]);
    fs.writeFileSync(path.join(d.cwd, "src/shift.ts"), "export function shiftTimesEditor() {\n  return 1;\n}\n");
    git(d.cwd, "add", "-A");
    git(d.cwd, "commit", "-qm", "shift");
    expect((await runCli(["index", "build", "--json"], d)).exitCode).toBe(0);
    const ring0 = ring0Name(d);
    const scoped = async (out: string, sources = "file,code") => {
      const io = scriptedIo([MAP, NONE]);
      const r = await makeScopeCommand(io)([briefFile(), "--out", out, "--sources", sources], d);
      return { r, input: io.inputs[0] ?? "" };
    };
    const inOther = await scoped(path.join(other, "scopes"));
    expect(inOther.r.exitCode).toBe(0);
    expect(inOther.input).toContain('ref="code:other/');
    expect(inOther.input).not.toContain(`ref="code:${ring0}/`);
    const inRing0 = await scoped(path.join(d.cwd, "scopes"));
    expect(inRing0.input).toContain(`ref="code:${ring0}/`);
    expect(inRing0.input).not.toContain('ref="code:other/');
    const outside = await scoped(tempDir());
    expect(outside.input).toContain('ref="code:other/');
    expect(outside.input).toContain(`ref="code:${ring0}/`);
    const stranger = gitRepo({ "x.md": "x\n" });
    expect((await scoped(path.join(stranger, "scopes"))).r.stderr).toContain("SND-SCOPE-025");
    expect(fs.existsSync(path.join(stranger, "scopes"))).toBe(false);
    const fileOnly = await scoped(path.join(stranger, "scopes"), "file");
    expect(fileOnly.r.exitCode).toBe(0);
    expect(fileOnly.input).not.toContain('kind="code"');
  });
});

describe("the public-repo guard fails closed", () => {
  const NOT_A_REPO = { ok: false as const, stderr: "fatal: not a git repository (or any of the parent directories): .git\n", code: 128 };

  it("refuses notes when git can't answer (dubious ownership, git missing), but allows --sources file,code outside any repo", async () => {
    const d = await approvedScopeDeps();
    const f = briefFile();
    const cases = [
      { ok: false as const, stderr: "fatal: detected dubious ownership in repository at '/x'\n", code: 128 },
      { ok: false as const, stderr: "spawn git ENOENT" },
    ];
    for (const answer of cases) {
      const gd = { ...d, git: fakeGit({ "rev-parse --show-toplevel": answer }) };
      const out = path.join(tempDir(), "maps");
      expect((await makeScopeCommand(scriptedIo([]))([f, "--out", out, "--sources", "file,notes"], gd)).stderr).toContain(
        "SND-SCOPE-025 refusing to write maps: could not confirm --out is outside a git worktree (git failed), and the map may carry notes",
      );
      expect((await makeScopeCommand(scriptedIo([]))([f, "--out", out, "--sources", "file,code"], gd)).stderr).toContain("SND-SCOPE-025");
      expect(fs.existsSync(out)).toBe(false);
      expect((await makeScopeCommand(scriptedIo([MAP, NONE]))([f, "--out", out, "--sources", "file"], gd)).exitCode).toBe(0);
    }
    const plain = { ...d, git: fakeGit({ "rev-parse --show-toplevel": NOT_A_REPO }) };
    expect((await makeScopeCommand(scriptedIo([MAP, NONE]))([f, "--out", tempDir()], plain)).exitCode).toBe(0);
  });

  it("treats a .git directory or file found walking up from --out as a worktree, and refuses inside a .git dir", async () => {
    const d = await approvedScopeDeps();
    const f = briefFile();
    const plain = { ...d, git: fakeGit({ "rev-parse --show-toplevel": NOT_A_REPO }) };
    const withDir = tempDir();
    fs.mkdirSync(path.join(withDir, ".git"));
    const withFile = tempDir();
    fs.writeFileSync(path.join(withFile, ".git"), "gitdir: /elsewhere\n");
    for (const root of [withDir, withFile]) {
      expect((await makeScopeCommand(scriptedIo([]))([f, "--out", path.join(root, "a", "b")], plain)).stderr).toContain("SND-SCOPE-025 refusing to write b inside a git worktree");
      expect((await makeScopeCommand(scriptedIo([]))([f, "--out", path.join(root, "a"), "--sources", "file,code"], plain)).stderr).toContain("SND-SCOPE-025");
    }
    expect((await makeScopeCommand(scriptedIo([]))([f, "--out", path.join(d.cwd, ".git", "maps")], d)).stderr).toContain("SND-SCOPE-025");
  });

  it("refuses --sources file,code in a non-profile repo nested inside a profile repo", async () => {
    const d = await approvedScopeDeps();
    const nested = path.join(d.cwd, "vendor", "inner");
    fs.mkdirSync(nested, { recursive: true });
    git(nested, "init", "-q", "-b", "main");
    const r = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", path.join(nested, "maps"), "--sources", "file,code"], d);
    expect(r.stderr).toContain("SND-SCOPE-025");
  });

  it("follows a symlinked --out into a worktree and refuses", async () => {
    const d = await approvedScopeDeps();
    const link = path.join(tempDir(), "link");
    fs.symlinkSync(d.cwd, link);
    expect((await makeScopeCommand(scriptedIo([]))([briefFile(), "--out", path.join(link, "maps")], d)).stderr).toContain("SND-SCOPE-025");
    expect(fs.existsSync(path.join(d.cwd, "maps"))).toBe(false);
  });
});

describe("writing and recording", () => {
  it("writeOut never leaves an orphan .md when only the .json name is taken", () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "scope-x.json"), "{}");
    const file = writeOut(dir, "scope-x", "# x\n", {}, makeScrubber());
    expect(path.basename(file)).toBe("scope-x-2.md");
    expect(fs.readdirSync(dir).sort()).toEqual(["scope-x-2.json", "scope-x-2.md", "scope-x.json"]);
  });

  it("prints the written file, then the error, when the run can't be recorded", async () => {
    const d = await approvedScopeDeps();
    const db = openLedger(ledgerPath(stateDir(d)));
    db.pragma("user_version = 99");
    db.close();
    const out = tempDir();
    const r = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", out], d);
    expect(r.exitCode).toBe(2);
    expect(r.stdout).toContain(`Wrote ${path.join(out, "scope-shift-times-2026-10-08.md")}`);
    expect(r.stderr).toContain("SND-LEDGER-001");
    expect(fs.readdirSync(out)).toHaveLength(2);
    const j = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", out, "--json"], d);
    expect(JSON.parse(j.stdout).error.details.join("\n")).toContain("scope-shift-times-2026-10-08-2.md");
    const broken = await approvedScopeDeps();
    const bdb = openLedger(ledgerPath(stateDir(broken)));
    bdb.exec("DROP TABLE model_calls; DROP TABLE scope_runs;");
    bdb.close();
    const b = await makeScopeCommand(scriptedIo([MAP, NONE]))([briefFile(), "--out", out], broken);
    expect(b.stdout).toContain("scope-shift-times-2026-10-08-3.md");
    expect(b.stderr).toContain("SND-CLI-900 could not record the run: no such table: scope_runs");
  });

  it("a failing, is_error or over-budget model still writes the file and records an incomplete run with its model calls", async () => {
    const usage = { inputTokens: 40, outputTokens: 15 };
    const cases: { answers: unknown[]; extra: string; calls: number }[] = [
      { answers: [new Error("claude exited 1")], extra: "", calls: 0 },
      { answers: [new ModelJobError("model job reported an error: overloaded", usage)], extra: "", calls: 1 },
      { answers: [{ ...MAP, workstreams: [] }, MAP], extra: "scope:\n  maxTokensPerRun: 55\n", calls: 1 },
    ];
    for (const c of cases) {
      const d = await approvedScopeDeps("", c.extra);
      const out = tempDir();
      const r = await makeScopeCommand(scriptedIo(c.answers))([briefFile(), "--out", out], d);
      expect(r.exitCode).toBe(1);
      expect(fs.readdirSync(out)).toHaveLength(2);
      expect(rows(d, "SELECT status FROM scope_runs")).toEqual([{ status: "incomplete" }]);
      expect(rows(d, "SELECT COUNT(*) AS n FROM model_calls")).toEqual([{ n: c.calls }]);
    }
  });

  it("never writes the Linear token to stdout, the map files or the ledger", async () => {
    const token = "lin_" + "api_" + "Zq7Kx2Wm9Rt4Yp6Ln3Bv8Hc5Jd1Fs0Ga";
    const d = await approvedScopeDeps("  linear:\n    token: env:LINEAR_TOKEN\n");
    const auth: string[] = [];
    const fetchFor: GraphqlFetch = async (_u, init) => {
      auth.push(init.headers.authorization);
      const q = JSON.parse(init.body) as { query: string };
      if (q.query.includes("projects(")) return { ok: true, status: 200, json: async () => ({ data: { projects: { nodes: [{ id: "p1", name: "Shift times", description: "shift times", createdAt: "2026-01-01T00:00:00Z", url: "u" }] } } }) };
      return { ok: true, status: 200, json: async () => ({ data: { project: { issues: { pageInfo: { hasNextPage: false, endCursor: null }, nodes: [] } } } }) };
    };
    const out = tempDir();
    const r = await makeScopeCommand(scriptedIo([MAP, NONE], fetchFor))(["linear:abc", "--out", out], { ...d, env: { ...d.env, LINEAR_TOKEN: token } });
    expect(r.exitCode).toBe(0);
    expect(auth.length).toBeGreaterThan(0);
    expect(auth.every((a) => a === token)).toBe(true);
    const files = fs.readdirSync(out).map((n) => fs.readFileSync(path.join(out, n), "utf8")).join("\n");
    const ledger = JSON.stringify([rows(d, "SELECT * FROM scope_runs"), rows(d, "SELECT * FROM model_calls")]);
    for (const text of [r.stdout, r.stderr, files, ledger]) expect(text).not.toContain(token.slice(8));
  });
});
