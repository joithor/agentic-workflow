import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { runCli } from "../src/main.js";
import { hitsIn, hookBinary, parseAddedLines, preCommitHook, preCommitPath, PRE_COMMIT_MARKER } from "../src/scrub/commands.js";
import { makeScrubber } from "../src/scrub/scrub.js";
import { realGitRunner } from "../src/git-real.js";
import { fakeGit, makeDeps, tempDir } from "./helpers.js";

function repo(): string {
  const root = tempDir("sindri-scrub-");
  execFileSync("git", ["init", "-q"], { cwd: root });
  return root;
}

function stage(root: string, rel: string, text: string): void {
  fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
  fs.writeFileSync(path.join(root, rel), text);
  execFileSync("git", ["add", rel], { cwd: root });
}

describe("parseAddedLines", () => {
  it("maps added lines to new line numbers by counting hunks (Review Focus: '++ ' text)", () => {
    const diff = [
      "diff --git a/x.ts b/x.ts", "--- a/x.ts", "+++ b/x.ts", "@@ -1,0 +2,3 @@", "+one", "+++ looks like a header", "+two",
      "@@ -9 +12 @@", "-gone", "+twelve", "\\ No newline at end of file",
      "diff --git a/y b/y", "--- a/y", "+++ /dev/null", "@@ -1 +0,0 @@", "-bye",
      "diff --git a/z b/z", "--- a/z", "+++ b/z", "@@ -1,2 +1,2 @@", " same", "-old", "+new",
    ].join("\n");
    expect(parseAddedLines(diff)).toEqual([
      { file: "x.ts", lines: [{ line: 2, text: "one" }, { line: 3, text: "++ looks like a header" }, { line: 4, text: "two" }, { line: 12, text: "twelve" }] },
      { file: "z", lines: [{ line: 2, text: "new" }] },
    ]);
  });

  it("finds a private key split over added lines, at its first line", () => {
    const f = { file: "k.pem", lines: [{ line: 1, text: "intro" }, { line: 2, text: "-----BEGIN " + "PRIVATE KEY-----" }, { line: 3, text: "MIIabc" }, { line: 4, text: "-----END PRIVATE KEY-----" }] };
    expect(hitsIn(f, makeScrubber())).toEqual([{ file: "k.pem", line: 2, kind: "private-key" }]);
  });
});

describe("sindri scrub --staged", () => {
  it("passes clean changes and refuses secrets without printing them", async () => {
    const root = repo();
    stage(root, "ok.txt", "nothing to see\n");
    const clean = await runCli(["scrub", "--staged"], makeDeps({ cwd: root }));
    expect(clean).toEqual({ exitCode: 0, stdout: "No secrets in staged changes.\n", stderr: "" });
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    stage(root, "src/conf.ts", `const a = 1;\nconst key = "${secret}";\n`);
    const r = await runCli(["scrub", "--staged"], makeDeps({ cwd: root }));
    expect(r.exitCode).toBe(1);
    expect(r.stderr).toContain("SND-SCRUB-002 refused: 1 likely secret(s) in staged changes:");
    expect(r.stderr).toContain("  src/conf.ts:2 aws-access-key");
    expect(r.stderr).not.toContain(secret);
    const json = JSON.parse((await runCli(["scrub", "--staged", "--json"], makeDeps({ cwd: root }))).stdout);
    expect(json.error).toMatchObject({ code: "SND-SCRUB-002", details: ["src/conf.ts:2 aws-access-key"] });
  });

  it("scans files git would show as binary (NUL bytes, -diff attribute)", async () => {
    const root = repo();
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    stage(root, ".gitattributes", "*.dat -diff\n");
    stage(root, "blob.dat", `k=${secret}\n`);
    stage(root, "nul.txt", `\u0000\u0001 k=${secret}\n`);
    const r = await runCli(["scrub", "--staged"], makeDeps({ cwd: root }));
    expect(r.stderr).toContain("blob.dat:1 aws-access-key");
    expect(r.stderr).toContain("nul.txt:1 aws-access-key");
  });

  it("scans a renamed-and-edited file", async () => {
    const root = repo();
    stage(root, "a.txt", "line one\nline two\nline three\n");
    execFileSync("git", ["-c", "user.name=T", "-c", "user.email=t@example.com", "commit", "-qm", "a"], { cwd: root });
    execFileSync("git", ["mv", "a.txt", "b.txt"], { cwd: root });
    stage(root, "b.txt", `line one\nline two\nline three\nkey ${"AKIA" + "ABCDEFGHIJKLMNOP"}\n`);
    const r = await runCli(["scrub", "--staged"], makeDeps({ cwd: root }));
    expect(r.stderr).toContain("b.txt:4 aws-access-key");
  });

  it("refuses (exit 2) when the profile is invalid and has no approved snapshot, never falling back to built-ins", async () => {
    const root = repo();
    stage(root, "ok.txt", "fine\n");
    const deps = makeDeps({ cwd: root });
    await runCli(["profile", "init"], deps);
    fs.appendFileSync(path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml"), "\nbogus: 1\n");
    const r = await runCli(["scrub", "--staged"], deps);
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-PROFILE-001 the profile at");
    expect(r.stderr).toContain("has no approved snapshot");
  });

  it("uses the approved snapshot's patterns, so an unapproved edit can't drop or break them", async () => {
    const root = repo();
    stage(root, "ids.txt", "EMP-123456\n");
    const deps = makeDeps({ cwd: root });
    await runCli(["profile", "init"], deps);
    const file = path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    const original = fs.readFileSync(file, "utf8");
    fs.appendFileSync(file, '\nscrub:\n  extraPatterns:\n    - kind: employee-id\n      regex: "\\\\bEMP-\\\\d{6}\\\\b"\n');
    const hash = JSON.parse((await runCli(["profile", "approve", "--json"], deps)).stdout).hash as string;
    const ok = await runCli(["profile", "approve", hash], { ...deps, isTTY: true, prompt: async () => hash.slice(0, 6) });
    expect(ok.exitCode).toBe(0);
    const ledger = path.join(deps.env.AW_STATE_DIR as string, "sindri", "ledger.db");
    const beside = (): string[] => fs.readdirSync(path.dirname(ledger)).filter((n) => n.startsWith("ledger.db"));
    const before = beside();
    // Unapproved edit that drops the pattern: the approved one still applies.
    fs.writeFileSync(file, original);
    const dropped = await runCli(["scrub", "--staged"], deps);
    expect(dropped.exitCode).toBe(1);
    expect(dropped.stderr).toContain("ids.txt:1 employee-id");
    expect(dropped.stderr).toContain(`note: using approved profile ${hash.slice(0, 12)}; the live profile has unapproved changes`);
    // Unapproved edit that breaks the profile: still the approved patterns.
    fs.appendFileSync(file, "\nbogus: 1\n");
    const broken = await runCli(["scrub", "--staged"], deps);
    expect(broken.exitCode).toBe(1);
    expect(broken.stderr).toContain("ids.txt:1 employee-id");
    expect(beside()).toEqual(before);
  });

  it("reports a git failure other than 'not a repo' with git's stderr (SND-SCRUB-005)", async () => {
    const r = await runCli(["scrub", "--staged"], makeDeps({ git: fakeGit({ "rev-parse --git-dir": { ok: true, stdout: ".git\n" } }) }));
    expect(r.exitCode).toBe(2);
    expect(r.stderr).toContain("SND-SCRUB-005 git diff --cached failed");
    expect(r.stderr).toContain("unexpected git call: -c core.quotePath=false diff --cached");
  });

  it("uses the profile's extra patterns when a profile loads", async () => {
    const root = repo();
    stage(root, "ids.txt", "EMP-123456\n");
    const deps = makeDeps({ cwd: root });
    await runCli(["profile", "init"], deps);
    const file = path.join(deps.env.AW_STATE_DIR as string, "profile", "profile.yaml");
    fs.appendFileSync(file, '\nscrub:\n  extraPatterns:\n    - kind: employee-id\n      regex: "\\\\bEMP-\\\\d{6}\\\\b"\n');
    const r = await runCli(["scrub", "--staged"], deps);
    expect(r.stderr).toContain("ids.txt:1 employee-id");
  });

  it("fails outside a git repo", async () => {
    const r = await runCli(["scrub", "--staged"], makeDeps({ cwd: tempDir() }));
    expect(r.stderr).toContain("SND-SCRUB-004");
  });
});

describe("sindri scrub --install-pre-commit", () => {
  it("installs an executable hook, reinstalls idempotently, and refuses a foreign hook", async () => {
    const root = repo();
    const deps = makeDeps({ cwd: root });
    const r = await runCli(["scrub", "--install-pre-commit"], deps);
    expect(r.exitCode).toBe(0);
    const hook = path.join(root, ".git/hooks/pre-commit");
    expect(fs.readFileSync(hook, "utf8")).toContain(PRE_COMMIT_MARKER);
    expect(hookBinary(fs.readFileSync(hook, "utf8"))).toBe("sindri");
    expect(fs.statSync(hook).mode & 0o111).not.toBe(0);
    expect((await runCli(["scrub", "--install-pre-commit"], deps)).exitCode).toBe(0);
    fs.writeFileSync(hook, "#!/bin/sh\necho mine\n");
    expect((await runCli(["scrub", "--install-pre-commit"], deps)).stderr).toContain("SND-SCRUB-003");
  });

  it("resolves a relative core.hooksPath against the top level from a subdirectory, and defaults to .git/hooks", async () => {
    const root = repo();
    const sub = path.join(root, "pkg", "deep");
    fs.mkdirSync(sub, { recursive: true });
    const fallback = await runCli(["scrub", "--install-pre-commit"], makeDeps({ cwd: sub }));
    expect(fs.realpathSync(fallback.stdout.match(/at (.*)\.\n/)?.[1] as string)).toBe(fs.realpathSync(path.join(root, ".git/hooks/pre-commit")));
    execFileSync("git", ["config", "core.hooksPath", ".githooks"], { cwd: root });
    await runCli(["scrub", "--install-pre-commit"], makeDeps({ cwd: sub }));
    expect(fs.existsSync(path.join(root, ".githooks", "pre-commit"))).toBe(true);
    expect(fs.existsSync(path.join(sub, ".githooks"))).toBe(false);
  });

  it("honors core.hooksPath and --repo, and refuses outside git", async () => {
    const root = repo();
    execFileSync("git", ["config", "core.hooksPath", ".githooks"], { cwd: root });
    const real = fs.realpathSync(root); // git reports symlink-resolved paths (macOS /var -> /private/var)
    expect(await preCommitPath(realGitRunner(), root)).toBe(path.join(real, ".githooks", "pre-commit"));
    const r = await runCli(["scrub", "--install-pre-commit", "--repo", root], makeDeps());
    expect(r.stdout).toContain(path.join(real, ".githooks", "pre-commit"));
    expect((await runCli(["scrub", "--install-pre-commit"], makeDeps({ cwd: tempDir() }))).stderr).toContain("SND-SCRUB-004");
  });
});

describe("the installed hook", () => {
  it("records the absolute CLI path it was installed with, quoting safely", () => {
    expect(hookBinary(preCommitHook("/home/o'neil/.local/bin/sindri"))).toBe("/home/o'neil/.local/bin/sindri");
    expect(hookBinary("#!/bin/sh\necho mine\n")).toBeNull();
  });

  it("fails closed when the CLI is missing (Review Focus: no silent skip)", () => {
    const root = repo();
    const hook = path.join(root, "hook.sh");
    fs.writeFileSync(hook, preCommitHook(path.join(root, "no-such-sindri")));
    let code = 0;
    let err = "";
    try {
      execFileSync("sh", [hook], { cwd: root, stdio: ["ignore", "ignore", "pipe"] });
    } catch (e) {
      code = (e as { status: number }).status;
      err = String((e as { stderr: Buffer }).stderr);
    }
    expect(code).toBe(1);
    expect(err).toContain("refusing the commit");
  });

  it("installs with $SINDRI_BIN when the wrapper sets it", async () => {
    const root = repo();
    await runCli(["scrub", "--install-pre-commit"], makeDeps({ cwd: root, env: { SINDRI_BIN: "/opt/bin/sindri" } }));
    expect(hookBinary(fs.readFileSync(path.join(root, ".git/hooks/pre-commit"), "utf8"))).toBe("/opt/bin/sindri");
  });
});

describe("sindri scrub (stdin)", () => {
  it("writes scrubbed text and counts hits on stderr", async () => {
    const secret = "ghp" + "_" + "q".repeat(36);
    const r = await runCli(["scrub"], makeDeps({ stdin: async () => `token ${secret}\n` }));
    expect(r.stdout).toBe("token [REDACTED:github-token]\n");
    expect(r.stderr).toBe("scrubbed 1 hit(s)\n");
    const none = await runCli(["scrub"], makeDeps({ stdin: async () => "plain\n" }));
    expect(none).toEqual({ exitCode: 0, stdout: "plain\n", stderr: "" });
  });

  it("--json prints the scrubbed text and the hits (kinds and offsets only)", async () => {
    const secret = "ghp" + "_" + "q".repeat(36);
    const r = await runCli(["scrub", "--json"], makeDeps({ stdin: async () => `token ${secret}\n` }));
    expect(r.exitCode).toBe(0);
    expect(r.stdout).not.toContain(secret);
    expect(JSON.parse(r.stdout)).toEqual({ text: "token [REDACTED:github-token]\n", hits: [{ kind: "github-token", start: 6, end: 46 }] });
  });
});
