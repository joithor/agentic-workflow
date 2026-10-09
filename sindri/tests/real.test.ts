import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { realGitRunner } from "../src/git-real.js";
import { sandboxArgv } from "../src/index/graph.js";
import { hasBinary, realIndexIo, realIndexProbes, realProcessRunner } from "../src/index/sandbox-real.js";
import { realSystemProbe } from "../src/system-real.js";

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
    const ok = await git.run(["--version"], process.cwd());
    expect(ok.ok && ok.stdout).toMatch(/^git version /);
    const bad = await git.run(["no-such-subcommand"], process.cwd());
    expect(bad.ok).toBe(false);
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

describe("sandbox (smoke)", () => {
  const o = { cwd: process.cwd(), timeoutMs: 15_000 };
  const box = (argv: string[]) => sandboxArgv(process.platform, argv, hasBinary, { writable: [], home: os.homedir(), exists: fs.existsSync });

  it("denies the network, once the same request is shown to succeed outside the sandbox", async () => {
    const run = realProcessRunner();
    const curl = ["curl", "-sS", "--max-time", "3", "https://example.com"];
    const wrapped = box(curl);
    if (wrapped === null || !hasBinary("curl")) return; // no sandbox or curl here: nothing to prove
    if ((await run.run(curl, o)).code !== 0) return; // offline: the control failed, so a sandboxed failure proves nothing
    expect((await run.run(wrapped, o)).code).not.toBe(0);
  });

  it("denies writes outside the snapshot and hides ~/.ssh", async () => {
    const run = realProcessRunner();
    // A fresh dir that is not writable in the sandbox (not a temp dir), never the real home.
    const dir = fs.mkdtempSync(path.join(import.meta.dirname, "..", ".sandbox-probe-"));
    try {
      const probe = path.join(dir, "probe");
      const write = box(["touch", probe]);
      if (write === null) return;
      expect((await run.run(["touch", path.join(dir, "control")], o)).code).toBe(0);
      expect((await run.run(write, o)).code).not.toBe(0);
      expect(fs.existsSync(probe)).toBe(false);
    } finally {
      fs.rmSync(dir, { recursive: true });
    }
    const ssh = path.join(os.homedir(), ".ssh");
    const read = box(["ls", "-A", ssh]);
    if (read !== null && fs.existsSync(ssh)) {
      const r = await run.run(read, o);
      expect(r.code !== 0 || r.stdout.trim() === "").toBe(true);
    }
  });
});
