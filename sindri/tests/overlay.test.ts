import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { realGitRunner } from "../src/git-real.js";
import { buildOverlay, stagedChanges } from "../src/index/overlay.js";
import { BODY, failingGit } from "./index-fixtures.js";
import { git, gitRepo } from "./helpers.js";

const caps = { denyPaths: [".env*"], maxFileKB: 512 };

describe("staged overlay", () => {
  it("reads staged contents (not the working tree), deletions and added lines", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n", "b.ts": "export const b = 1;\n", "logo.png": "x" });
    fs.writeFileSync(path.join(root, "a.ts"), "export function a() { return 2; }\n");
    git(root, "add", "a.ts");
    fs.writeFileSync(path.join(root, "a.ts"), "UNSTAGED EDIT\n");
    git(root, "rm", "-q", "b.ts");
    fs.writeFileSync(path.join(root, "logo.png"), Buffer.from([0, 1, 2, 0]));
    git(root, "add", "logo.png");
    const { changes, addedLines, skipped } = await stagedChanges(realGitRunner(), root, caps);
    expect(changes).toEqual([
      { path: "a.ts", text: "export function a() { return 2; }\n" },
      { path: "b.ts", text: null },
      { path: "logo.png", text: null },
    ]);
    expect(addedLines).toBe(1);
    expect(skipped).toEqual([]);
  });

  it("reports a rename's old path as deleted, so a moved file doesn't match itself", async () => {
    const root = gitRepo({ "src/old.ts": BODY("clip") });
    git(root, "mv", "src/old.ts", "src/new.ts");
    const { changes } = await stagedChanges(realGitRunner(), root, caps);
    expect(changes).toEqual([
      { path: "src/new.ts", text: BODY("clip") },
      { path: "src/old.ts", text: null },
    ]);
  });

  it("skips denied paths and over-cap blobs, and keeps a huge generated file out of the parse (Review Focus 5)", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n" });
    fs.writeFileSync(path.join(root, "a.ts"), "export const a = 2;\n");
    fs.writeFileSync(path.join(root, ".env.ts"), "export const secret = 1;\n");
    fs.writeFileSync(path.join(root, "gen.ts"), "export const v = () => 1;\n".repeat(80_000));
    git(root, "add", "-A");
    const { changes, skipped, addedLines } = await stagedChanges(realGitRunner(), root, caps);
    expect(changes.map((c) => [c.path, c.text === null ? null : "text"])).toEqual([["a.ts", "text"], ["gen.ts", null]]);
    expect(skipped).toEqual([{ path: ".env.ts", reason: "denied" }, { path: "gen.ts", reason: "too-large" }]);
    expect(addedLines).toBe(80_002);
    expect(buildOverlay(changes, addedLines).symbols).toEqual([]);
  });

  it("treats a blob it can't size or read as unreadable, and survives a failing numstat", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n" });
    fs.writeFileSync(path.join(root, "a.ts"), "export const a = 2;\n");
    git(root, "add", "a.ts");
    const sizeFails = await stagedChanges(failingGit("cat-file"), root, caps);
    expect(sizeFails.changes).toEqual([{ path: "a.ts", text: null }]);
    expect(sizeFails.skipped).toEqual([{ path: "a.ts", reason: "unreadable" }]);
    const showFails = await stagedChanges(failingGit("show"), root, caps);
    expect(showFails.changes).toEqual([{ path: "a.ts", text: null }]);
    expect(showFails.skipped).toEqual([]);
    expect((await stagedChanges(failingGit("--numstat"), root, caps)).addedLines).toBe(0);
  });

  it("parses only code and manifests into the overlay", () => {
    const o = buildOverlay([
      { path: "src/x.ts", text: "export function x(a: number) { return a + 1; }\n" },
      { path: "src/gone.ts", text: null },
      { path: "package.json", text: "{}" },
      { path: "README.md", text: "# r" },
    ], 3);
    expect(o.symbols.map((s) => s.name)).toEqual(["x"]);
    expect(o.symbols[0].minhash).toHaveLength(64);
    expect([...o.changedPaths].sort()).toEqual(["README.md", "package.json", "src/gone.ts", "src/x.ts"]);
    expect(o.manifests).toEqual([{ path: "package.json", text: "{}" }]);
    expect(o.addedLines).toBe(3);
  });

  it("stops parsing once the deadline passes and records the rest as skipped (Task 9 I1)", () => {
    const fn = (n: string) => ({ path: `src/${n}.ts`, text: `export function ${n}(a: number) { return a + 1; }\n` });
    let t = 0;
    const o = buildOverlay([fn("a"), { path: "src/gone.ts", text: null }, fn("b"), fn("c")], 3, new Map(), { at: 1, now: () => t++ });
    expect(o.symbols.map((s) => s.name)).toEqual(["a", "b"]);
    expect(o.skipped).toEqual([{ path: "src/c.ts", reason: "over-budget" }]);
    expect(buildOverlay([fn("a")], 1).skipped).toEqual([]);
  });

  it("fails outside a git repo", async () => {
    await expect(stagedChanges(realGitRunner(), "/", caps)).rejects.toThrow(/SND-INDEX-002|not a git repo/);
  });
});

describe("staged overlay refusals", () => {
  it("refuses a staged symlink and matches deny paths case-insensitively", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n" });
    fs.symlinkSync("../outside.ts", path.join(root, "link.ts"));
    fs.writeFileSync(path.join(root, "SECRETS.ENV"), "x\n");
    git(root, "add", "-A");
    const r = await stagedChanges(realGitRunner(), root, { denyPaths: ["*.env"], maxFileKB: 512 });
    expect(r.changes).toEqual([{ path: "link.ts", text: null }]);
    expect(r.skipped).toEqual([{ path: "SECRETS.ENV", reason: "denied" }, { path: "link.ts", reason: "symlink" }]);
  });
});

describe("staged overlay reads only what it parses", () => {
  it("never reads a blob for non-code files, however many or large", async () => {
    const root = gitRepo({ "a.ts": "export const a = 1;\n" });
    for (let i = 0; i < 20; i++) fs.writeFileSync(path.join(root, `img${i}.png`), Buffer.concat([Buffer.from([0]), Buffer.alloc(300_000, i + 1)]));
    fs.writeFileSync(path.join(root, "notes.md"), "# n\n".repeat(1000));
    git(root, "add", "-A");
    const real = realGitRunner();
    const calls: string[][] = [];
    const spy = { run: async (args: string[], cwd: string) => { calls.push(args); return real.run(args, cwd); } };
    const { changes, addedLines } = await stagedChanges(spy, root, caps);
    expect(changes.every((c) => c.text === null)).toBe(true);
    expect(changes).toHaveLength(21);
    expect(addedLines).toBe(1000);
    expect(calls.filter((a) => a.includes("cat-file") || a.includes("show"))).toEqual([]);
  });
});
