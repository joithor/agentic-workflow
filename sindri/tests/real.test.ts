import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { realGitRunner } from "../src/git-real.js";
import { pathKind, PRIVATE_DIR, privateEnv, sandboxArgv } from "../src/index/graph.js";
import { hasBinary, realIndexIo, realIndexProbes, realProcessRunner } from "../src/index/sandbox-real.js";
import { realSystemProbe } from "../src/system-real.js";
import { realSpawner } from "../src/scope/model-real.js";

describe("realSystemProbe (smoke)", () => {
  it("answers for this process on this host", () => {
    const sys = realSystemProbe();
    expect(sys.hostname()).toBe(os.hostname());
    expect(sys.pid).toBe(process.pid);
    expect(sys.pidAlive(process.pid)).toBe(true);
    expect(sys.pidStartTime(process.pid)).not.toBeNull();
    if (process.platform === "darwin" || process.platform === "linux") expect(sys.bootId()).not.toBeNull();
    expect(sys.isLocalDisk(os.tmpdir())).not.toBe(false);
  });
});

describe("realGitRunner (smoke)", () => {
  it("runs git and reports failures", async () => {
    const git = realGitRunner();
    const ok = await git.run(["--version"], process.cwd(), { timeoutMs: 5000 });
    expect(ok.ok && ok.stdout).toMatch(/^git version /);
    const bad = await git.run(["no-such-subcommand"], process.cwd());
    expect(bad.ok).toBe(false);
    // The exit status rides along: `git grep` exits 1 for "no match", other failures differently.
    const none = await git.run(["grep", "-l", "-F", "-e", "sindri-no-such-text-" + "xyz", "HEAD", "--"], process.cwd());
    expect(none.ok ? null : none.code).toBe(1);
    expect((await git.run(["--version"], process.cwd())).ok).toBe(true);
    const badRev = await git.run(["grep", "-l", "-F", "-e", "x", "no-such-rev", "--"], process.cwd());
    expect(badRev.ok ? null : badRev.code).toBe(128);
    // git never started (a missing cwd): no exit status, and the error message stands in for stderr.
    const unstarted = await git.run(["--version"], path.join(os.tmpdir(), "sindri-no-such-dir-" + process.pid));
    expect(unstarted.ok ? null : [unstarted.code, unstarted.stderr]).toEqual([undefined, expect.stringContaining("ENOENT")]);
  });

  it("clears git's repository variables for a call about another repo", async () => {
    const git = realGitRunner();
    const repo = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-hookrepo-"));
    const other = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-foreign-"));
    expect((await git.run(["init", "-q"], repo)).ok).toBe(true);
    process.env.GIT_DIR = path.join(repo, ".git");
    try {
      const inherited = await git.run(["rev-parse", "--absolute-git-dir"], other);
      expect(inherited.ok && fs.realpathSync(inherited.stdout.trim())).toBe(fs.realpathSync(path.join(repo, ".git")));
      const foreign = await git.run(["rev-parse", "--absolute-git-dir"], other, { foreign: true });
      expect(foreign.ok ? "" : foreign.stderr).toMatch(/not a git repository/);
    } finally {
      delete process.env.GIT_DIR;
    }
  });

  it("adds per-call environment variables, for that call only", async () => {
    const git = realGitRunner();
    const withEnv = await git.run(["var", "GIT_EDITOR"], process.cwd(), { env: { GIT_EDITOR: "sindri-editor" } });
    expect(withEnv.ok && withEnv.stdout.trim()).toBe("sindri-editor");
    const without = await git.run(["var", "GIT_EDITOR"], process.cwd());
    expect(without.ok && without.stdout.trim()).not.toBe("sindri-editor");
  });
});

describe("real index I/O (smoke)", () => {
  it("runs commands, cleans the environment on request, and finds binaries", async () => {
    const run = realProcessRunner();
    const o = { cwd: process.cwd(), timeoutMs: 5000 };
    expect((await run.run(["echo", "hi"], o)).stdout.trim()).toBe("hi");
    process.env.SINDRI_SMOKE_SECRET = "s3cret";
    try {
      const sh = ["sh", "-c", 'echo "[$SINDRI_SMOKE_SECRET]"'];
      expect((await run.run(sh, o)).stdout.trim()).toBe("[s3cret]");
      expect((await run.run(sh, { ...o, cleanEnv: true })).stdout.trim()).toBe("[]");
    } finally {
      delete process.env.SINDRI_SMOKE_SECRET;
    }
    expect((await run.run(["definitely-not-a-binary-xyz"], o)).code).not.toBe(0);
    expect(hasBinary("sh")).toBe(true);
    expect(hasBinary("definitely-not-a-binary-xyz")).toBe(false);
    expect((await run.run(["sh", "-c", "exit 3"], o)).code).toBe(3);
    // A key that isn't set stays unset; `env` is set over the clean environment.
    const lang = process.env.LANG;
    delete process.env.LANG;
    try {
      const env = ["sh", "-c", 'echo "${LANG-unset} $SINDRI_SMOKE_X"'];
      expect((await run.run(env, { ...o, cleanEnv: true, env: { SINDRI_SMOKE_X: "x" } })).stdout.trim()).toBe("unset x");
    } finally {
      if (lang !== undefined) process.env.LANG = lang;
    }
  });

  it("hasBinary takes an absolute path only when it is a root-owned executable file", () => {
    expect(hasBinary("/bin/sh")).toBe(true);
    const mine = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "sindri-bin-")), "sandbox-exec");
    fs.writeFileSync(mine, "#!/bin/sh\n", { mode: 0o755 });
    expect(hasBinary(mine)).toBe(process.getuid?.() === 0);
    expect(hasBinary("/bin")).toBe(false);
    expect(hasBinary("/no/such/binary")).toBe(false);
    fs.rmSync(path.dirname(mine), { recursive: true });
  });

  it("getJson answers JSON from a loopback server, and null for an error, a redirect and a closed port", async () => {
    const server = http.createServer((req, res) => {
      if (req.url === "/ok") {
        res.setHeader("content-type", "application/json");
        res.end('{"models":[]}');
      } else if (req.url === "/redirect") {
        res.statusCode = 302;
        res.setHeader("location", "/ok");
        res.end();
      } else {
        res.statusCode = 500;
        res.end();
      }
    });
    await new Promise<void>((r) => server.listen(0, "127.0.0.1", r));
    const port = (server.address() as { port: number }).port;
    const probes = realIndexProbes();
    expect((await probes.run(["echo", "ok"], { cwd: process.cwd(), timeoutMs: 5000 })).stdout.trim()).toBe("ok");
    try {
      expect(await probes.getJson(`http://127.0.0.1:${port}/ok`, 2000)).toEqual({ models: [] });
      expect(await probes.getJson(`http://127.0.0.1:${port}/err`, 2000)).toBeNull();
      expect(await probes.getJson(`http://127.0.0.1:${port}/redirect`, 2000)).toBeNull();
    } finally {
      await new Promise((r) => server.close(r));
    }
    expect(await probes.getJson(`http://127.0.0.1:${port}/ok`, 500)).toBeNull();
    expect(typeof realIndexIo().fetch).toBe("function");
  });
});

// Each test skips (and says so in the report) when this machine can't prove its point; a
// skipped sandbox test is unverified, never green.
describe("sandbox (smoke)", () => {
  const o = { cwd: process.cwd(), timeoutMs: 15_000 };
  const box = (argv: string[], writable: string[] = []) =>
    sandboxArgv(process.platform, argv, hasBinary, { writable, home: os.homedir(), kind: pathKind, runtimeDir: process.env.XDG_RUNTIME_DIR });
  const noSandbox = box(["true"]) === null;

  it("finds the sandbox by an absolute, root-owned path", () => {
    if (process.platform === "darwin") expect(box(["true"])?.[0]).toBe("/usr/bin/sandbox-exec");
    else if (!noSandbox) expect(box(["true"])?.[0]).toMatch(/^\/(usr\/)?bin\/bwrap$/);
  });

  it.skipIf(noSandbox || !hasBinary("curl"))("denies the network, once the same request is shown to succeed outside the sandbox", async (ctx) => {
    const run = realProcessRunner();
    const curl = ["curl", "-sS", "--max-time", "3", "https://example.com"];
    if ((await run.run(curl, o)).code !== 0) ctx.skip(); // offline: the control failed, so a sandboxed failure proves nothing
    expect((await run.run(box(curl) ?? [], o)).code).not.toBe(0);
  });

  it.skipIf(process.platform !== "darwin")("denies LaunchServices opens (a browser launched outside the sandbox would carry data out)", async (ctx) => {
    const run = realProcessRunner();
    // `open -R /` exits 0 even when LaunchServices refuses it, so the probe is a background
    // open of Finder (always running; -g keeps it in the background), shown to work outside first.
    const open = ["/usr/bin/open", "-g", "-a", "Finder"];
    if ((await run.run(open, o)).code !== 0) ctx.skip(); // no window server (headless): a sandboxed failure proves nothing
    const r = await run.run(box(open) ?? [], o);
    expect(r.code).not.toBe(0);
    expect(r.stderr).toContain("-54");
  });

  it.skipIf(process.platform !== "darwin")("denies submitting a launchd job (it would run outside the sandbox)", async (ctx) => {
    const run = realProcessRunner();
    const dir = fs.mkdtempSync(path.join(import.meta.dirname, "..", ".sandbox-probe-"));
    const label = `sindri.smoke-probe.${process.pid}`;
    const submit = (file: string) => ["/bin/launchctl", "submit", "-l", label, "--", "/usr/bin/touch", file];
    try {
      const control = await run.run(submit(path.join(dir, "control")), o);
      await run.run(["/bin/launchctl", "remove", label], o);
      if (control.code !== 0) ctx.skip(); // no launchd session here: a sandboxed failure proves nothing
      const r = await run.run(box(submit(path.join(dir, "probe"))) ?? [], o);
      if (r.code === 0) await run.run(["/bin/launchctl", "remove", label], o);
      expect(r.code).not.toBe(0);
    } finally {
      fs.rmSync(dir, { recursive: true });
    }
  });

  it.skipIf(noSandbox)("denies writes outside the snapshot: a plain dir, ~/.cache and the system temp dir; the private dir is writable", async () => {
    const run = realProcessRunner();
    // Fresh dirs, removed after: a probe that got through never stays behind.
    const dir = fs.mkdtempSync(path.join(import.meta.dirname, "..", ".sandbox-probe-"));
    const cache = path.join(os.homedir(), ".cache");
    const cacheProbe = fs.existsSync(cache) ? fs.mkdtempSync(path.join(cache, "sindri-sandbox-probe-")) : null;
    const tmpProbe = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-sandbox-probe-"));
    const snap = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), "sindri-graph-")));
    try {
      for (const target of [dir, cacheProbe, tmpProbe, fs.realpathSync(tmpProbe)]) {
        if (target === null) continue;
        expect((await run.run(["touch", path.join(target, "control")], o)).code).toBe(0);
        expect((await run.run(box(["touch", path.join(target, "probe")], [snap]) ?? [], o)).code).not.toBe(0);
        expect(fs.existsSync(path.join(target, "probe"))).toBe(false);
      }
      // Inside the snapshot, through the private env, writes work (what graphify's temp files use).
      const priv = path.join(snap, PRIVATE_DIR);
      fs.mkdirSync(priv);
      const sh = box(["sh", "-c", 'touch "$TMPDIR/t" && mkdir -p "$XDG_CACHE_HOME" && touch "$XDG_CACHE_HOME/c"'], [snap]) ?? [];
      expect((await run.run(sh, { ...o, cleanEnv: true, env: privateEnv(priv) })).code).toBe(0);
      expect(fs.existsSync(path.join(priv, "t")) && fs.existsSync(path.join(priv, "cache", "c"))).toBe(true);
    } finally {
      for (const d of [dir, cacheProbe, tmpProbe, snap]) if (d !== null) fs.rmSync(d, { recursive: true });
    }
  });

  it.skipIf(noSandbox || !fs.existsSync(path.join(os.homedir(), ".ssh")))("hides ~/.ssh", async () => {
    const r = await realProcessRunner().run(box(["ls", "-A", path.join(os.homedir(), ".ssh")]) ?? [], o);
    expect(r.code !== 0 || r.stdout.trim() === "").toBe(true);
  });
});

describe("realSpawner (smoke)", () => {
  it("pipes stdin, and kills on timeout", async () => {
    const run = realSpawner();
    expect(await run(["cat"], { stdin: "hello", cwd: process.cwd(), timeoutMs: 5000, env: process.env })).toMatchObject({ code: 0, stdout: "hello", timedOut: false });
    expect((await run(["sleep", "5"], { stdin: "", cwd: process.cwd(), timeoutMs: 200, env: process.env })).timedOut).toBe(true);
  });

  it("returns code 127 for a missing binary instead of crashing on the closed stdin", async () => {
    const r = await realSpawner()(["sindri-no-such-binary"], { stdin: "hello", cwd: process.cwd(), timeoutMs: 5000, env: process.env });
    expect(r).toMatchObject({ code: 127, timedOut: false });
  });
});
