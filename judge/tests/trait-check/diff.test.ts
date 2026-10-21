import { describe, expect, it } from "vitest";

import { chunkFile, ignoredTraits, parseDiff } from "../../src/trait-check/diff.js";

const DIFF = [
  "diff --git a/src/a.ts b/src/a.ts",
  "index 1..2 100644",
  "--- a/src/a.ts",
  "+++ b/src/a.ts",
  "@@ -1,2 +1,3 @@ header",
  " keep",
  "+added",
  "-removed",
  "\\ No newline at end of file",
  "@@ -10 +11 @@",
  "+single",
  "diff --git a/gone.ts b/gone.ts",
  "deleted file mode 100644",
  "--- a/gone.ts",
  "+++ /dev/null",
  "@@ -1 +0,0 @@",
  "-bye",
  "diff --git a/img.png b/img.png",
  "Binary files a/img.png and b/img.png differ",
  "diff --git a/new.ts b/new.ts",
  "--- /dev/null",
  "+++ b/new.ts",
  "@@ -0,0 +1,2 @@",
  "+one",
  "+two",
  "",
].join("\n");

describe("parseDiff", () => {
  it("keeps changed files with their hunks and skips deleted and binary files", () => {
    expect(parseDiff(DIFF)).toEqual([
      {
        path: "src/a.ts",
        hunks: [
          { start: 1, end: 3, text: "@@ -1,2 +1,3 @@ header\n keep\n+added\n-removed\n\\ No newline at end of file" },
          { start: 11, end: 11, text: "@@ -10 +11 @@\n+single" },
        ],
      },
      { path: "new.ts", hunks: [{ start: 1, end: 2, text: "@@ -0,0 +1,2 @@\n+one\n+two" }] },
    ]);
  });

  it("treats a pure-deletion hunk as one line wide", () => {
    const out = parseDiff("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -3,2 +2,0 @@\n-a\n-b");
    expect(out[0]?.hunks[0]).toMatchObject({ start: 2, end: 2 });
  });
});

describe("chunkFile", () => {
  const file = { path: "p.ts", hunks: [{ start: 1, end: 2, text: "aaaa" }, { start: 5, end: 6, text: "bbbb" }, { start: 9, end: 9, text: "cccccccc" }] };

  it("merges consecutive hunks while they fit", () => {
    expect(chunkFile(file, 9)).toEqual([
      { path: "p.ts", startLine: 1, endLine: 6, text: "aaaa\nbbbb" },
      { path: "p.ts", startLine: 9, endLine: 9, text: "cccccccc" },
    ]);
  });

  it("splits an oversized hunk by lines and truncates an oversized line", () => {
    const big = { path: "p.ts", hunks: [{ start: 1, end: 3, text: "1234\n5678\n123456789" }] };
    expect(chunkFile(big, 6).map((s) => s.text)).toEqual(["1234", "5678", "123456"]);
  });

  it("gives each piece of a split hunk its own new-file line span", () => {
    const text = ["@@ -0,0 +10,4 @@", "+aaaa", "+bbbb", "-gone", "+cccc", "+dddd"].join("\n");
    const big = { path: "p.ts", hunks: [{ start: 10, end: 13, text }] };
    expect(chunkFile(big, 24).map((s) => [s.startLine, s.endLine])).toEqual([[10, 10], [11, 13]]);
  });

  it("pins a deletion-only piece to the line where it sits", () => {
    const text = ["@@ -5,3 +5,1 @@", "-xxxxxxxx", "-yyyyyyyy", " keep"].join("\n");
    const big = { path: "p.ts", hunks: [{ start: 5, end: 5, text }] };
    expect(chunkFile(big, 20).map((s) => [s.startLine, s.endLine])).toEqual([[5, 5], [5, 5], [5, 5]]);
  });

  it("never inverts the span of trailing deletion pieces", () => {
    const text = ["@@ -5,3 +5,1 @@", " keep", "-xxxxxxxx", "-yyyyyyyy"].join("\n");
    const big = { path: "p.ts", hunks: [{ start: 5, end: 5, text }] };
    expect(chunkFile(big, 20).map((s) => [s.startLine, s.endLine])).toEqual([[5, 5], [5, 5], [5, 5]]);
  });
});

describe("ignoredTraits", () => {
  it("reads directives with a reason from the leading comment block only", () => {
    const head = "#!/usr/bin/env node\n\n// trait-ignore-file static-imports-first: generated\n// trait-ignore-file no-reason\n// plain comment\nimport x from 'x';\n// trait-ignore-file late-one: too late";
    expect([...ignoredTraits(head)]).toEqual(["static-imports-first"]);
  });
});
