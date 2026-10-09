import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { discover, globMatch, isEvalMachinery, isProtectedPath, loadRegistry, normalizeRepoPath, saveRegistry } from "../src/evolve/registry.js";
import { realGitRunner } from "../src/git-real.js";
import { bumpEpoch, openMemoryLedger } from "../src/ledger/db.js";
import { git } from "./evolve-fixtures.js";
import { gitRepo } from "./helpers.js";

const FILES = {
  "skills/review/SKILL.md": "---\nname: review\n---\n",
  "skills/ui-evidence/SKILL.md": "---\nname: ui-evidence\n---\n",
  "skills/ui-evidence/package.json": "{}",
  "skills/_shared/capabilities.md": "# caps\n",
  "skills/_preamble.md": "# preamble\n",
  "config/hooks/done-gate.sh": "#!/bin/sh\n",
  "config/hooks/block-destructive.sh": "#!/bin/sh\n",
  "config/hooks/git-context.sh": "#!/bin/sh\n",
  "config/lib/tests/done-gate.test.sh": "#!/bin/sh\n",
  "judge/package.json": "{}",
  "sindri/package.json": "{}",
  "sindri/src/observe/observe.ts": "export const a = 1;\n",
  "sindri/src/scrub/patterns.ts": "export const b = 2;\n",
  "sindri/src/evolve/blind.ts": "export const c = 3;\n",
  "providers/claude/install.sh": "#!/bin/sh\n",
  "providers/tests/install.test.sh": "#!/bin/sh\n",
  "setup.sh": "#!/bin/sh\n",
  "scripts/sync-rules.sh": "#!/bin/sh\n",
  ".agents/rules/testing.md": "# t\n",
  "planning/ARCHITECTURE.md": "# a\n",
  "mods/aw-live/plugin.json": "{}",
  "EXTERNAL_PINS.env": "X=1\n",
  "skills/_design-preamble.md": "# design preamble\n",
  "bootstrap/SKILL.md": "---\nname: bootstrap\n---\n",
  "config/hooks/adapters/codex.sh": "#!/bin/sh\n",
  "config/hooks/tests/codex-adapter.test.sh": "#!/bin/sh\n",
  "scripts/install-sindri.sh": "#!/bin/sh\n",
  "scripts/tests/install-sindri.test.sh": "#!/bin/sh\n",
};

describe("artifact registry (spec §7.7)", () => {
  it("discovers every module class with its eval suite, root and protection", async () => {
    const a = await discover(realGitRunner(), gitRepo(FILES), [{ id: "scope.draft", text: "draft prompt" }]);
    expect(a.map((x) => x.id)).toEqual([
      "doc:architecture", "doc:skills-preamble", "doc:skills-shared", "hook:adapters", "hook:block-destructive", "hook:done-gate", "hook:git-context",
      "installer:providers-claude", "installer:setup", "installer:sindri", "mod:aw-live", "pack-pin:external", "package:judge", "package:sindri",
      "prompt:scope.draft", "rule:testing", "skill:bootstrap", "skill:review", "skill:ui-evidence",
    ]);
    const by = Object.fromEntries(a.map((x) => [x.id, x]));
    expect(by["hook:done-gate"]).toMatchObject({ protected: false, root: null, suite: { argv: ["bash", "config/lib/tests/done-gate.test.sh"], cwd: "." } });
    expect(by["hook:done-gate"].paths).toEqual(["config/hooks/done-gate.sh", "config/lib/tests/done-gate.test.sh"]);
    expect(by["hook:block-destructive"]).toMatchObject({ protected: true, suite: null });
    expect(by["hook:git-context"]).toMatchObject({ protected: false, suite: null });
    expect(by["package:judge"]).toMatchObject({ protected: false, root: "judge/", suite: { argv: ["npm", "test"], cwd: "judge" } });
    // Protected paths inside a package don't make the package protected (they still route proposals to approval).
    expect(by["package:sindri"]).toMatchObject({ protected: false, root: "sindri/" });
    expect(by["package:sindri"].paths).toEqual(expect.arrayContaining(["sindri/src/scrub/patterns.ts", "sindri/src/evolve/blind.ts"]));
    expect(by["skill:ui-evidence"]).toMatchObject({ root: "skills/ui-evidence/", suite: { argv: ["npm", "test"], cwd: "skills/ui-evidence" } });
    expect(by["skill:review"]).toMatchObject({ root: "skills/review/", suite: null });
    expect(by["rule:testing"]).toMatchObject({ protected: true, suite: { argv: ["scripts/sync-rules.sh", "--check"], cwd: "." } });
    expect(by["mod:aw-live"].suite).toEqual({ argv: ["claude", "plugin", "test", "mods/aw-live"], cwd: "." });
    expect(by["installer:providers-claude"]).toMatchObject({ protected: true, suite: { argv: ["bash", "providers/tests/install.test.sh"], cwd: "." } });
    expect(by["installer:setup"]).toMatchObject({ protected: true, suite: { argv: ["./setup.sh", "--providers", "claude,codex,cursor", "--dry-run"], cwd: "." } });
    expect(by["doc:architecture"]).toMatchObject({ suite: null, protected: false });
    expect(by["prompt:scope.draft"]).toMatchObject({ kind: "prompt", paths: [], root: null, protected: false, suite: null });
    expect(by["doc:skills-preamble"]).toMatchObject({ kind: "doc", root: null, suite: null, protected: false });
    expect(by["doc:skills-preamble"].paths).toEqual(["skills/_design-preamble.md", "skills/_preamble.md"]);
    expect(by["skill:bootstrap"]).toMatchObject({ root: "bootstrap/", suite: null, protected: false, paths: ["bootstrap/SKILL.md"] });
    expect(by["hook:adapters"]).toMatchObject({
      protected: true, root: "config/hooks/adapters/", suite: { argv: ["bash", "config/hooks/tests/codex-adapter.test.sh"], cwd: "." },
      paths: ["config/hooks/adapters/codex.sh", "config/hooks/tests/codex-adapter.test.sh"],
    });
    expect(by["installer:sindri"]).toMatchObject({
      protected: true, paths: ["scripts/install-sindri.sh"], suite: { argv: ["bash", "scripts/tests/install-sindri.test.sh"], cwd: "." },
    });
    expect(by["skill:review"].hash).toMatch(/^[0-9a-f]{64}$/);
  });

  it("hashes a prompt by its effective text, and applies the repo profile's protected globs to hooks", async () => {
    const root = gitRepo(FILES);
    const one = await discover(realGitRunner(), root, [{ id: "scope.draft", text: "one" }]);
    const two = await discover(realGitRunner(), root, [{ id: "scope.draft", text: "two" }], ["config/hooks/**"]);
    const hash = (list: typeof one) => list.find((x) => x.id === "prompt:scope.draft")?.hash;
    expect(hash(one)).not.toBe(hash(two));
    expect(two.find((x) => x.id === "hook:done-gate")?.protected).toBe(true);
    expect(two.find((x) => x.id === "hook:git-context")?.protected).toBe(true);
    expect(one.find((x) => x.id === "hook:git-context")?.protected).toBe(false);
  });

  it("omits suites whose test scripts aren't tracked", async () => {
    const a = await discover(realGitRunner(), gitRepo({ "providers/claude/install.sh": "#!/bin/sh\n", ".agents/rules/x.md": "# x\n" }), []);
    expect(a.map((x) => [x.id, x.suite])).toEqual([["installer:providers-claude", null], ["rule:x", null]]);
  });

  it("finds an installer's suite under config/lib/tests, and registers adapters without a suite", async () => {
    const a = await discover(realGitRunner(), gitRepo({
      "scripts/install-foo.sh": "#!/bin/sh\n", "config/lib/tests/install-foo.test.sh": "#!/bin/sh\n", "scripts/install-bar.sh": "#!/bin/sh\n",
      "config/hooks/adapters/x.sh": "#!/bin/sh\n",
    }), []);
    const by = Object.fromEntries(a.map((x) => [x.id, x]));
    expect(by["installer:foo"].suite).toEqual({ argv: ["bash", "config/lib/tests/install-foo.test.sh"], cwd: "." });
    expect(by["installer:bar"].suite).toBeNull();
    expect(by["hook:adapters"]).toMatchObject({ suite: null, paths: ["config/hooks/adapters/x.sh"] });
  });

  it("ignores tracked files that are missing or aren't regular files", async () => {
    const root = gitRepo({ "skills/review/SKILL.md": "x\n", "skills/gone/SKILL.md": "y\n" });
    fs.rmSync(path.join(root, "skills/gone/SKILL.md"));
    fs.symlinkSync("SKILL.md", path.join(root, "skills/review/link"));
    git(root, "add", "skills/review/link");
    const a = await discover(realGitRunner(), root, []);
    expect(a.map((x) => x.id)).toEqual(["skill:review"]);
    expect(a[0].paths).toEqual(["skills/review/SKILL.md"]);
  });

  it("refuses a directory that isn't a readable git repo", async () => {
    await expect(discover({ run: async () => ({ ok: false, stderr: "fatal" }) }, "/nope", [])).rejects.toThrow(/SND-EVOLVE-001|not a readable git repository/);
  });
});

describe("path rules (Review Focus 4)", () => {
  it("normalizes repo paths and rejects anything that could escape or hide", () => {
    expect(normalizeRepoPath("a/b")).toBe("a/b");
    expect(normalizeRepoPath("./a//b")).toBe("a/b");
    expect(normalizeRepoPath("a/./b")).toBe("a/b");
    for (const bad of ["", "x".repeat(201), "a b", "a\nb", "/a", "a/../b", "..", ".", "dir/", "./"]) expect(normalizeRepoPath(bad)).toBeNull();
  });

  it("marks protected and eval-machinery paths, case-insensitively and after normalizing", () => {
    for (const p of [
      "config/hooks/detect-secrets.sh", "sindri/src/scrub/patterns.ts", "sindri/src/evolve/anything.ts", "providers/claude/install.sh", "setup.sh",
      "scripts/install-sindri.sh", "scripts/sync-rules.sh", "AGENTS.md", ".agents/rules/testing.md", "sindri/tests/anything.ts",
      "skills/ui-evidence/tests/x.ts", "judge/vitest.config.ts", "sindri/package.json", "sindri/src/scope/map.ts", "sindri/src/scope/gather.ts",
      "config/hooks/tests/foo.test.sh", "./sindri//src/evolve/blind.ts", "Sindri/Src/Evolve/Blind.ts", "sindri/src/secrets.ts",
      "sindri/src/profile/approve.ts", "config/lib/x.sh", "skills/_shared/capabilities.md", "sindri/src/lock/lock.ts",
    ]) expect(isProtectedPath(p), p).toBe(true);
    for (const invalid of ["../x", "/etc/passwd", "a\nb", "a/../b", "", "dir/"]) expect(isProtectedPath(invalid), invalid).toBe(true);
    for (const p of ["skills/review/SKILL.md", "sindri/src/observe/observe.ts", "config/hooks/git-context.sh", "planning/ARCHITECTURE.md", "sindri/src/scope/run.ts"]) {
      expect(isProtectedPath(p), p).toBe(false);
    }
    expect(isProtectedPath("config/hooks/git-context.sh", ["config/hooks/**"])).toBe(true);
    expect(isEvalMachinery("sindri/src/evolve/blind.ts")).toBe(true);
    expect(isEvalMachinery("skills/review/tests/x.sh")).toBe(true);
    expect(isEvalMachinery("scripts/sync-rules.sh")).toBe(true);
    expect(isEvalMachinery("../x")).toBe(true);
    expect(isEvalMachinery("config/hooks/detect-secrets.sh")).toBe(false);
    expect(isEvalMachinery("sindri/src/observe/observe.ts")).toBe(false);
  });

  it("matches globs with * (one segment) and ** (any depth)", () => {
    expect(globMatch(".github/**", ".github/workflows/ci.yml")).toBe(true);
    expect(globMatch("config/hooks/*.sh", "config/hooks/a.sh")).toBe(true);
    expect(globMatch("config/hooks/*.sh", "config/hooks/x/a.sh")).toBe(false);
    expect(globMatch("a.b", "aXb")).toBe(false);
    expect(globMatch("src/**/x.ts", "SRC/deep/er/x.ts")).toBe(true);
  });
});

describe("saveRegistry and loadRegistry", () => {
  it("reports added, changed and removed artifacts and round-trips them", async () => {
    const db = openMemoryLedger();
    const epoch = bumpEpoch(db);
    const now = new Date("2026-10-08T00:00:00Z");
    const a = await discover(realGitRunner(), gitRepo(FILES), []);
    expect(saveRegistry(db, a, epoch, now)).toEqual({ added: a.length, changed: 0, removed: 0 });
    expect(loadRegistry(db)).toEqual(a);
    const changed = a.map((x) => (x.id === "skill:review" ? { ...x, hash: "f".repeat(64) } : x)).filter((x) => x.id !== "mod:aw-live");
    expect(saveRegistry(db, changed, epoch, now)).toEqual({ added: 0, changed: 1, removed: 1 });
    const again = loadRegistry(db);
    expect(again.map((x) => x.id)).not.toContain("mod:aw-live");
    expect(again.find((x) => x.id === "skill:review")?.hash).toBe("f".repeat(64));
    expect(saveRegistry(db, a, epoch, now)).toEqual({ added: 1, changed: 1, removed: 0 });
  });
});
