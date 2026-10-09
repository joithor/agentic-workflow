import http from "node:http";
import os from "node:os";
import { describe, expect, it } from "vitest";

import { realGitRunner } from "../src/git-real.js";
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
