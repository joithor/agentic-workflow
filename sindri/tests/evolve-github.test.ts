import { describe, expect, it } from "vitest";
import { z } from "zod";

import { allowedAuthors, ghJson, ghRepoOf, parseRemote, prContext } from "../src/evolve/github.js";
import type { GitRunner } from "../src/git.js";
import { fakeProc } from "./evolve-fixtures.js";

const git = (url: string | null): GitRunner => ({ run: async () => (url === null ? { ok: false, stderr: "no remote" } : { ok: true, stdout: `${url}\n` }) });

describe("parseRemote and ghRepoOf", () => {
  it("reads owner/name from GitHub remotes", async () => {
    for (const u of ["git@github.com:acme/toolkit.git", "https://github.com/acme/toolkit", "https://github.com/acme/toolkit.git/", "ssh://git@github.com/acme/toolkit.git"]) {
      expect(parseRemote(u), u).toBe("acme/toolkit");
    }
    for (const u of ["https://gitlab.com/acme/toolkit.git", "/local/path", "https://github.com/acme", ""]) expect(parseRemote(u), u).toBeNull();
    expect(await ghRepoOf(git("git@github.com:acme/toolkit.git"), "/r")).toBe("acme/toolkit");
    await expect(ghRepoOf(git("https://gitlab.com/a/b"), "/r")).rejects.toThrow(/no GitHub remote called origin/);
    await expect(ghRepoOf(git(null), "/r")).rejects.toThrow(/no GitHub remote called origin/);
  });
});

describe("ghJson", () => {
  const Shape = z.object({ n: z.number() });
  it("validates the reply and turns failures into typed errors, scrubbing stderr", async () => {
    expect(await ghJson(fakeProc(() => ({ stdout: '{"n":3}' })), ["gh", "x"], "/r", Shape)).toEqual({ n: 3 });
    await expect(ghJson(fakeProc(() => ({ code: 1, stderr: `boom ${"AKIA" + "ABCDEFGHIJKLMNOP"}\nsecond line` })), ["gh", "pr", "view"], "/r", Shape)).rejects.toThrow("gh pr view failed: boom [REDACTED:aws-access-key]");
    await expect(ghJson(fakeProc(() => ({ stdout: "not json" })), ["gh", "pr", "view"], "/r", Shape)).rejects.toThrow(/isn't JSON/);
    await expect(ghJson(fakeProc(() => ({ stdout: '{"n":"three"}' })), ["gh", "pr", "view"], "/r", Shape)).rejects.toThrow(/unexpected shape/);
  });
});

const view = (over: Record<string, unknown> = {}) =>
  JSON.stringify({ title: "T", body: "B", headRefName: "feat/x", files: [{ path: "a.ts" }], state: "MERGED", mergedAt: "2026-10-07T00:00:00Z", author: { login: "joi-t" }, ...over });

describe("prContext (read-only, repo pinned, merged PRs by allowed authors only)", () => {
  it("reads the PR with gh pinned to the repo, scrubbed", async () => {
    const proc = fakeProc((argv) => (argv.includes("view") ? { stdout: view({ body: null }) } : { stdout: `diff --git a/a.ts\n+key ${"AKIA" + "ABCDEFGHIJKLMNOP"}` }));
    const c = await prContext(proc, "/repo", "acme/toolkit", 12, ["joi-t"]);
    expect(proc.calls.map((x) => x.argv)).toEqual([
      ["gh", "pr", "view", "12", "--repo", "acme/toolkit", "--json", "title,body,headRefName,files,state,mergedAt,author"],
      ["gh", "pr", "diff", "12", "--repo", "acme/toolkit"],
    ]);
    expect(c).toMatchObject({ title: "T", body: "", branch: "feat/x", files: ["a.ts"], author: "joi-t" });
    expect(c.diff).toContain("[REDACTED:aws-access-key]");
  });

  it("refuses unmerged PRs, other authors, an unreadable diff and a failing gh", async () => {
    await expect(prContext(fakeProc(() => ({ stdout: view({ state: "OPEN", mergedAt: null }) })), "/r", "a/b", 5, ["joi-t"])).rejects.toThrow(/PR #5 isn't merged/);
    await expect(prContext(fakeProc(() => ({ stdout: view({ state: "MERGED", mergedAt: null }) })), "/r", "a/b", 5, ["joi-t"])).rejects.toThrow(/PR #5 isn't merged/);
    await expect(prContext(fakeProc(() => ({ stdout: view({ author: { login: "mallory" } }) })), "/r", "a/b", 5, ["joi-t"])).rejects.toThrow(/PR #5 was written by mallory/);
    await expect(prContext(fakeProc((argv) => (argv.includes("view") ? { stdout: view() } : { code: 1, stderr: "nope" })), "/r", "a/b", 5, ["joi-t"])).rejects.toThrow(/couldn't read PR #5's diff/);
    await expect(prContext(fakeProc(() => ({ code: 1, stderr: "not found" })), "/r", "a/b", 99, ["joi-t"])).rejects.toThrow(/gh pr view failed: not found/);
  });

  it("uses the configured authors, or else the authenticated gh user", async () => {
    const proc = fakeProc(() => ({ stdout: '{"login":"joi-t"}' }));
    expect(await allowedAuthors(proc, "/r", ["a", "b"])).toEqual(["a", "b"]);
    expect(proc.calls).toHaveLength(0);
    expect(await allowedAuthors(proc, "/r", [])).toEqual(["joi-t"]);
    expect(proc.calls[0].argv).toEqual(["gh", "api", "user"]);
  });
});
