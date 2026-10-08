import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

import { SindriError } from "../src/errors.js";
import { inventory, isGraphInput, isSourcePath } from "../src/index/files.js";
import { realGitRunner } from "../src/git-real.js";
import { tempDir } from "./helpers.js";

afterEach(() => vi.restoreAllMocks());

function repo(files: Record<string, string>, links: Record<string, string> = {}): string {
  const root = tempDir("sindri-inv-");
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  for (const [rel, target] of Object.entries(links)) fs.symlinkSync(target, path.join(root, rel));
  execFileSync("git", ["init", "-q"], { cwd: root });
  execFileSync("git", ["add", "-A"], { cwd: root });
  return root;
}

const opts = { denyPaths: [".env*", "**/secrets/**"], maxFileKB: 1, maxTotalMB: 1, select: isSourcePath };

describe("inventory (Review Focus 1)", () => {
  it("lists tracked source files with hashes, skipping denied paths, symlinks and big files", async () => {
    const root = repo(
      { "src/a.ts": "export const a = 1;\n", "src/b.d.ts": "declare const b: number;\n", "src/secrets/k.ts": "x", ".env.ts": "x", "big.ts": "x".repeat(2000), "README.md": "# hi\n" },
      { "src/link.ts": "a.ts" },
    );
    fs.writeFileSync(path.join(root, "src/untracked.ts"), "export {};\n");
    const inv = await inventory(realGitRunner(), root, opts);
    expect(inv.files.map((f) => f.path)).toEqual(["src/a.ts"]);
    expect(inv.files[0]).toMatchObject({ size: 20, text: "export const a = 1;\n" });
    expect(inv.files[0].hash).toMatch(/^[0-9a-f]{64}$/);
    expect(inv.skipped).toEqual([
      { path: ".env.ts", reason: "denied" },
      { path: "big.ts", reason: "too-large" },
      { path: "src/link.ts", reason: "symlink" },
      { path: "src/secrets/k.ts", reason: "denied" },
    ]);
  });

  it("skips a tracked file that is gone, became a directory, or turned into a symlink before the read", async () => {
    const root = repo({ "a.ts": "1", "b.ts": "2", "c.ts": "3" });
    fs.rmSync(path.join(root, "a.ts"));
    fs.rmSync(path.join(root, "b.ts"));
    fs.mkdirSync(path.join(root, "b.ts"));
    const real = fs.openSync;
    vi.spyOn(fs, "openSync").mockImplementation((p, flags, mode) => {
      if (String(p).endsWith("c.ts")) throw Object.assign(new Error("loop"), { code: "ELOOP" });
      return real(p, flags, mode);
    });
    const inv = await inventory(realGitRunner(), root, opts);
    expect(inv.files).toEqual([]);
    expect(inv.skipped).toEqual([
      { path: "a.ts", reason: "unreadable" },
      { path: "b.ts", reason: "not-a-file" },
      { path: "c.ts", reason: "unreadable" },
    ]);
  });

  it("stops past maxTotalMB and outside a git repo", async () => {
    const root = repo({ "a.ts": "x".repeat(900), "b.ts": "y".repeat(900) });
    const err = await inventory(realGitRunner(), root, { ...opts, maxTotalMB: 0.001 }).catch((e: unknown) => e);
    expect((err as SindriError).code).toBe("SND-INDEX-003");
    const err2 = await inventory(realGitRunner(), tempDir(), opts).catch((e: unknown) => e);
    expect((err2 as SindriError).code).toBe("SND-INDEX-002");
  });

  it("isSourcePath accepts TS and JS, not declarations or other files", () => {
    for (const p of ["a.ts", "a.tsx", "a.mts", "a.cts", "a.js", "a.jsx", "a.mjs", "a.cjs"]) expect(isSourcePath(p)).toBe(true);
    for (const p of ["a.d.ts", "a.md", "package.json", "a.ts.bak"]) expect(isSourcePath(p)).toBe(false);
  });

  it("isGraphInput accepts source and docs, not data files or secrets", () => {
    for (const p of ["a.ts", "pkg/mod.py", "main.go", "README.md", "a.d.ts"]) expect(isGraphInput(p)).toBe(true);
    for (const p of ["data.sqlite", "package-lock.json", "key.pem", "notes.txt", "a.ts.bak"]) expect(isGraphInput(p)).toBe(false);
  });
});
