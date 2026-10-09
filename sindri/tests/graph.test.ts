import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { isReservedSnapshotPath, makeGraphifyProvider, parseGraphJson, pathKind, PRIVATE_DIR, privateEnv, sandboxArgv, type PathKind } from "../src/index/graph.js";
import type { ProcessRunner } from "../src/index/io.js";
import { tempDir } from "./helpers.js";

const FIXTURE = path.resolve(import.meta.dirname, "fixtures/graphify/graph.json");
const HOME = "/home/u";

describe("parseGraphJson", () => {
  it("reads the recorded graphify fixture", () => {
    const g = parseGraphJson(fs.readFileSync(FIXTURE, "utf8"));
    expect(g.nodes.length).toBeGreaterThanOrEqual(2);
    expect(g.edges.length).toBeGreaterThanOrEqual(1);
    expect(g.nodes.some((n) => n.file !== null && n.file.endsWith("a.ts"))).toBe(true);
    expect(g.nodes.some((n) => n.file !== null && n.name !== null)).toBe(true);
    // graphify writes source_location as "L<line>".
    expect(g.nodes.find((n) => n.name === "a()")?.line).toBe(1);
    expect(g.edges.some((e) => e.relation === "calls" && e.confidence === "EXTRACTED")).toBe(true);
  });

  it("accepts the field-name variants and defaults missing fields", () => {
    const g = parseGraphJson(JSON.stringify({
      nodes: [{ id: 1, label: "f", source_file: "a.ts", source_location: "a.ts:12" }, { id: "n2", name: "g", file: "b.ts", line: 3 }, { id: "n3" }],
      edges: [{ source: 1, target: "n2", relation: "calls", confidence: "EXTRACTED" }, { source: "n2", target: "n3", type: "imports" }, { source: "n3", target: 1 }],
    }));
    expect(g.nodes).toEqual([
      { id: "1", file: "a.ts", name: "f", line: 12 },
      { id: "n2", file: "b.ts", name: "g", line: 3 },
      { id: "n3", file: null, name: null, line: null },
    ]);
    expect(g.edges).toEqual([
      { src: "1", dst: "n2", relation: "calls", confidence: "EXTRACTED" },
      { src: "n2", dst: "n3", relation: "imports", confidence: "UNKNOWN" },
      { src: "n3", dst: "1", relation: "related", confidence: "UNKNOWN" },
    ]);
    expect(parseGraphJson("{}")).toEqual({ nodes: [], edges: [] });
  });

  it("rejects text that is not a JSON object, with SND-INDEX-008", () => {
    for (const text of ["not json", "null", "[]", "3"]) expect(() => parseGraphJson(text)).toThrow(SindriError);
    expect(() => parseGraphJson("null")).toThrow("graphify wrote an unexpected graph.json");
    expect(() => parseGraphJson("not json")).toThrow("graphify wrote invalid JSON");
    // A node without a usable id, or an edge without usable endpoints, is an error (never "undefined").
    const bad = [{ nodes: [null] }, { nodes: [{ id: "a" }, [1]] }, { links: [1] }, { edges: ["x"] }, { nodes: [{ label: "f" }] }, { nodes: [{ id: { x: 1 } }] }, { nodes: "x" }, { links: [{ source: "a" }] }, { edges: [{ source: "a", target: [1] }] }];
    for (const g of bad) {
      expect(() => parseGraphJson(JSON.stringify(g))).toThrow("graphify wrote an unexpected graph.json");
    }
  });
});

describe("sandboxArgv (Review Focus 4)", () => {
  const MAC_SECRETS = [".ssh", ".aws", ".gnupg", ".agentic-workflow", ".config/gh", ".docker", ".kube", ".codex", ".claude", ".claude.json", ".netrc", ".git-credentials", ".npmrc", ".pypirc", "Library/Keychains"];
  const o = { writable: ["/snap"], home: HOME, kind: (): PathKind => "dir" };

  it("wraps the command in a network-denying sandbox that writes only to the given paths, or refuses", () => {
    const hidden = MAC_SECRETS.map((d) => `(subpath "/home/u/${d}")`).join(" ");
    expect(sandboxArgv("darwin", ["graphify", "x"], () => true, o)).toEqual([
      "/usr/bin/sandbox-exec",
      "-p",
      `(version 1)(allow default)(deny network*)(deny lsopen)(deny appleevent-send)(deny mach-lookup (global-name "com.apple.coreservices.launchservicesd"))(deny process-exec (literal "/bin/launchctl"))(deny file-write*)(allow file-write* (subpath "/snap") (subpath "/dev"))(deny file-read* ${hidden})`,
      "graphify",
      "x",
    ]);
    // No ~/.cache, /private/tmp or /private/var/folders: other tools run code from them later.
    const profile = sandboxArgv("darwin", ["g"], () => true, o)?.[2] ?? "";
    for (const p of ["/home/u/.cache", "/private/tmp", "/private/var/folders"]) expect(profile).not.toContain(p);
    expect(sandboxArgv("darwin", ["g"], () => true, { ...o, kind: () => null })?.[2]).toBe(profile);
    // The sandbox binary by absolute path, never a bare name looked up on PATH.
    const asked: string[] = [];
    expect(sandboxArgv("darwin", ["g"], (b) => (asked.push(b), b === "sandbox-exec"), o)).toBeNull();
    expect(asked).toEqual(["/usr/bin/sandbox-exec"]);
    expect(sandboxArgv("linux", ["graphify"], () => false, o)).toBeNull();
    expect(sandboxArgv("linux", ["graphify"], (b) => b === "bwrap", o)).toBeNull();
    expect(sandboxArgv("darwin", ["graphify"], () => false, o)).toBeNull();
    expect(sandboxArgv("win32", ["graphify"], () => true, o)).toBeNull();
  });

  it("on Linux: unshares everything in a new session, masks /run and the runtime dir, and hides the credential stores that exist", () => {
    const kinds: Record<string, PathKind> = { "/run": "dir", "/var/run": "dir", "/xdg": "dir", "/home/u/.ssh": "dir", "/home/u/.netrc": "file" };
    const kind = (p: string): PathKind => kinds[p] ?? null;
    expect(sandboxArgv("linux", ["graphify", "x"], (b) => b === "/bin/bwrap", { ...o, kind, realpath: (p) => p, runtimeDir: "/xdg" })).toEqual([
      "/bin/bwrap", "--unshare-all", "--new-session", "--die-with-parent",
      "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
      "--tmpfs", "/run", "--tmpfs", "/var/run", "--tmpfs", "/xdg",
      "--tmpfs", "/home/u/.ssh", "--ro-bind", "/dev/null", "/home/u/.netrc",
      "--bind", "/snap", "/snap", "graphify", "x",
    ]);
    // A runtime dir under /run is already masked; /usr/bin/bwrap comes first.
    const argv = sandboxArgv("linux", ["g"], () => true, { ...o, kind, runtimeDir: "/run/user/1" }) ?? [];
    expect(argv[0]).toBe("/usr/bin/bwrap");
    expect(argv).not.toContain("/run/user/1");
    expect(argv).not.toContain("/home/u/.cache");
  });

  it("hides a symlinked credential dir by its real path too, and masks one target once", () => {
    const home = tempDir("sindri-home-");
    const real = fs.realpathSync(tempDir("sindri-ssh-"));
    fs.symlinkSync(real, path.join(home, ".ssh"));
    fs.symlinkSync(real, path.join(home, ".aws"));
    const profile = sandboxArgv("darwin", ["g"], () => true, { writable: [], home, kind: pathKind })?.[2] ?? "";
    expect(profile).toContain(`(subpath "${fs.realpathSync(home)}/.ssh")`);
    expect(profile).toContain(`(subpath "${real}")`);
    const linux = sandboxArgv("linux", ["g"], () => true, { writable: [], home, kind: pathKind }) ?? [];
    expect(linux.filter((a) => a === real)).toHaveLength(1);
    expect(linux).not.toContain(path.join(home, ".ssh"));
  });

  it("escapes quotes and backslashes in paths, and allows no write beyond /dev when no path is given", () => {
    const argv = sandboxArgv("darwin", ["g"], () => true, { writable: ['/a"b\\c'], home: "/h", kind: () => null });
    expect(argv?.[2]).toContain('(subpath "/a\\"b\\\\c")');
    expect(sandboxArgv("darwin", ["g"], () => true, { writable: [], home: "/h", kind: () => null })?.[2]).toContain('(allow file-write* (subpath "/dev"))');
  });

  it("names the home's real path in the macOS profile (a symlinked home would otherwise fail open)", () => {
    const real = fs.realpathSync(tempDir("sindri-realhome-"));
    const link = path.join(tempDir("sindri-linkhome-"), "home");
    fs.symlinkSync(real, link);
    const profile = sandboxArgv("darwin", ["g"], () => true, { writable: [], home: link, kind: pathKind })?.[2];
    expect(profile).toContain(`(subpath "${real}/.ssh")`);
    expect(profile).not.toContain(link);
  });

  it("pathKind follows symlinks; privateEnv points every temp and cache dir into the private dir", () => {
    const dir = tempDir();
    fs.writeFileSync(path.join(dir, "f"), "");
    fs.symlinkSync(path.join(dir, "f"), path.join(dir, "l"));
    expect([pathKind(dir), pathKind(path.join(dir, "f")), pathKind(path.join(dir, "l")), pathKind(path.join(dir, "missing"))]).toEqual(["dir", "file", "file", null]);
    expect(privateEnv("/s/.sindri-tmp")).toEqual({ TMPDIR: "/s/.sindri-tmp", XDG_CACHE_HOME: "/s/.sindri-tmp/cache", UV_CACHE_DIR: "/s/.sindri-tmp/cache/uv", PYTHONPYCACHEPREFIX: "/s/.sindri-tmp/pycache" });
    expect(PRIVATE_DIR).toBe(".sindri-tmp");
    expect(["graphify-out/a.md", "graphify-out", ".sindri-tmp/x.py", ".sindri-tmp-q/x.md", "src/a.ts", "src/graphify-out/a.ts", ".sindri-tmpx.md"].map(isReservedSnapshotPath)).toEqual([true, true, true, true, false, false, true]);
  });
});

describe("graphify provider", () => {
  type Opts = { cwd: string; cleanEnv?: boolean; env?: Record<string, string> };
  function runner(code: number, write: boolean | number | ((snap: string) => string)): ProcessRunner & { argv: string[][]; opts: Opts[] } {
    const argv: string[][] = [];
    const opts: Opts[] = [];
    return {
      argv,
      opts,
      run: async (a, o) => {
        argv.push(a);
        opts.push(o);
        if (write !== false) {
          fs.mkdirSync(path.join(o.cwd, "graphify-out"), { recursive: true });
          const body = typeof write === "function" ? write(o.cwd) : typeof write === "number" ? "x".repeat(write) : JSON.stringify({ nodes: [{ id: "a" }], links: [] });
          fs.writeFileSync(path.join(o.cwd, "graphify-out", "graph.json"), body);
        }
        return { code, stdout: "", stderr: code === 0 ? "" : "Traceback: boom\nmore" };
      },
    };
  }
  const make = (r: ProcessRunner, platform: NodeJS.Platform, has: (b: string) => boolean) =>
    makeGraphifyProvider({ bin: "graphify", version: "1.2.3", runner: r, platform, has, home: HOME, maxGraphMB: 32 });

  it("runs graphify sandboxed in the snapshot with a clean environment and parses its output", async () => {
    const r = runner(0, true);
    const p = make(r, "darwin", () => true);
    const snap = tempDir();
    expect((await p.build(snap)).nodes).toEqual([{ id: "a", file: null, name: null, line: null }]);
    const argv = r.argv[0];
    expect(argv.slice(0, 2)).toEqual(["/usr/bin/sandbox-exec", "-p"]);
    expect(argv[2]).toContain(`(subpath "${fs.realpathSync(snap)}")`);
    expect(argv.slice(3)).toEqual(["graphify", "extract", snap, "--code-only", "--no-viz"]);
    expect(r.opts[0]).toMatchObject({ cwd: snap, cleanEnv: true });
    // Temp and cache dirs: a fresh private dir with an unguessable name inside the snapshot, made before the run.
    const priv = r.opts[0].env?.TMPDIR ?? "";
    expect(path.dirname(priv)).toBe(fs.realpathSync(snap));
    expect(path.basename(priv)).toMatch(/^\.sindri-tmp-.{6}$/);
    expect(r.opts[0].env).toEqual(privateEnv(priv));
    expect(fs.readdirSync(priv)).toEqual(["cache"]);
    expect(argv[2]).not.toContain("/private/tmp");
    expect(p.version).toBe("1.2.3");
  });

  it("never reuses a pre-seeded private dir: repo files named like it are not graphify's temp or cache", async () => {
    const snap = tempDir();
    fs.mkdirSync(path.join(snap, ".sindri-tmp", "cache"), { recursive: true });
    fs.writeFileSync(path.join(snap, ".sindri-tmp", "cache", "x.py"), "planted");
    const r = runner(0, true);
    await make(r, "darwin", () => true).build(snap);
    const priv = r.opts[0].env?.TMPDIR ?? "";
    expect(priv).not.toBe(path.join(fs.realpathSync(snap), ".sindri-tmp"));
    expect(fs.readdirSync(path.join(priv, "cache"))).toEqual([]);
  });

    it("refuses a graph.json that is a symlink, a FIFO or not a file, or under a symlinked graphify-out", async () => {
    const secret = path.join(tempDir(), "hidden.json");
    fs.writeFileSync(secret, JSON.stringify({ nodes: [{ id: "leak" }] }));
    const plant = (how: (out: string) => void): ProcessRunner => ({
      run: async (_a, o) => {
        how(path.join(o.cwd, "graphify-out"));
        return { code: 0, stdout: "", stderr: "" };
      },
    });
    const build = (how: (out: string) => void) => make(plant(how), "darwin", () => true).build(tempDir());
    const unusable = "graphify wrote an unusable graph.json";
    await expect(build((out) => (fs.mkdirSync(out), fs.symlinkSync(secret, path.join(out, "graph.json"))))).rejects.toThrow(unusable);
    await expect(build((out) => fs.symlinkSync(path.dirname(secret), out))).rejects.toThrow(unusable);
    await expect(build((out) => fs.writeFileSync(out, "x"))).rejects.toThrow(unusable);
    await expect(build((out) => (fs.mkdirSync(out), fs.mkdirSync(path.join(out, "graph.json"))))).rejects.toThrow(unusable);
    await expect(build((out) => (fs.mkdirSync(out), execFileSync("mkfifo", [path.join(out, "graph.json")])))).rejects.toThrow(unusable);
    await expect(build((out) => fs.mkdirSync(out))).rejects.toThrow("graphify wrote no graph.json");
  });

  it("stores node files relative to the snapshot root; a path outside it is null", async () => {
    const files = (snap: string) => [`${snap}/src/a.ts`, `${fs.realpathSync(snap)}/src/b.ts`, "docs/r.md", "/src/c.ts", "/etc/passwd", "../up.ts", snap, `${snap}/../x.ts`, `${snap}/.sindri-tmp/cache/x.py`, ".sindri-tmp", `${snap}/.sindri-tmp-a1b2c3/x.py`, "graphify-out/graph.md"];
    const r = runner(0, (snap) => JSON.stringify({ nodes: files(snap).map((f, i) => ({ id: i, source_file: f })) }));
    const g = await make(r, "darwin", () => true).build(tempDir());
    expect(g.nodes.map((n) => n.file)).toEqual(["src/a.ts", "src/b.ts", "docs/r.md", null, null, null, null, null, null, null, null, null]);
  });

  it("reads the recorded fixture through the provider, with paths relative to the snapshot", async () => {
    const fixture = fs.readFileSync(FIXTURE, "utf8");
    const g = await make(runner(0, (snap) => fixture.replaceAll("/snapshot", snap)), "darwin", () => true).build(tempDir());
    expect([...new Set(g.nodes.map((n) => n.file))].sort()).toEqual(["a.ts", "b.ts"]);
  });

  it("fails closed without a sandbox, without graphify, on a failed run, and on a missing or huge graph.json", async () => {
    await expect(make(runner(0, true), "linux", (b) => b === "graphify").build(tempDir())).rejects.toThrow(/SND-INDEX-007|no network sandbox/);
    await expect(make(runner(0, true), "darwin", (b) => b !== "graphify").build(tempDir())).rejects.toThrow("graphify is not installed (sindri index setup)");
    await expect(make(runner(1, false), "darwin", () => true).build(tempDir())).rejects.toThrow("graphify failed (exit 1): Traceback: boom");
    await expect(make(runner(0, false), "darwin", () => true).build(tempDir())).rejects.toThrow("graphify wrote no graph.json");
  });

  it("reads a graph.json just under the limit and rejects one just over it, naming the key", async () => {
    const mk = (r: ProcessRunner) => makeGraphifyProvider({ bin: "graphify", version: "1.2.3", runner: r, platform: "darwin", has: () => true, home: HOME, maxGraphMB: 1 });
    const body = (bytes: number) => () => {
      const head = '{"nodes":[{"id":"a"}],"links":[],"pad":"';
      return head + "x".repeat(bytes - head.length - 2) + '"}';
    };
    expect((await mk(runner(0, body(1024 * 1024))).build(tempDir())).nodes).toHaveLength(1);
    await expect(mk(runner(0, body(1024 * 1024 + 1))).build(tempDir())).rejects.toThrow("graphify wrote a graph.json over 1 MB (raise index.graphMaxMB, max 512)");
  });
});
