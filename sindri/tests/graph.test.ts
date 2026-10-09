import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { makeGraphifyProvider, parseGraphJson, sandboxArgv } from "../src/index/graph.js";
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
  });
});

describe("sandboxArgv (Review Focus 4)", () => {
  const o = { writable: ["/snap"], home: HOME };

  it("wraps the command in a network-denying, filesystem-locked sandbox, or refuses", () => {
    expect(sandboxArgv("darwin", ["graphify", "x"], () => true, o)).toEqual([
      "sandbox-exec",
      "-p",
      '(version 1)(allow default)(deny network*)(deny file-write*)(allow file-write* (subpath "/snap") (subpath "/private/var/folders") (subpath "/private/tmp") (subpath "/dev") (subpath "/home/u/.cache"))(deny file-read* (subpath "/home/u/.ssh") (subpath "/home/u/.aws") (subpath "/home/u/.gnupg") (subpath "/home/u/.agentic-workflow") (subpath "/home/u/Library/Keychains"))',
      "graphify",
      "x",
    ]);
    expect(sandboxArgv("linux", ["graphify", "x"], () => true, o)).toEqual([
      "bwrap", "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp", "--bind", "/snap", "/snap",
      "--tmpfs", "/home/u/.ssh", "--tmpfs", "/home/u/.aws", "--tmpfs", "/home/u/.gnupg", "--tmpfs", "/home/u/.agentic-workflow",
      "--unshare-net", "--unshare-pid", "--die-with-parent", "graphify", "x",
    ]);
    expect(sandboxArgv("linux", ["graphify"], () => false, o)).toBeNull();
    expect(sandboxArgv("darwin", ["graphify"], () => false, o)).toBeNull();
    expect(sandboxArgv("win32", ["graphify"], () => true, o)).toBeNull();
  });

  it("escapes quotes and backslashes in paths, and allows no writable path beyond temp when none is given", () => {
    const argv = sandboxArgv("darwin", ["g"], () => true, { writable: ['/a"b\\c'], home: "/h" });
    expect(argv?.[2]).toContain('(subpath "/a\\"b\\\\c")');
    expect(sandboxArgv("darwin", ["g"], () => true, { writable: [], home: "/h" })?.[2]).toContain('(allow file-write* (subpath "/private/var/folders")');
  });
});

describe("graphify provider", () => {
  function runner(code: number, write: boolean | number): ProcessRunner & { argv: string[][]; opts: { cwd: string; cleanEnv?: boolean }[] } {
    const argv: string[][] = [];
    const opts: { cwd: string; cleanEnv?: boolean }[] = [];
    return {
      argv,
      opts,
      run: async (a, o) => {
        argv.push(a);
        opts.push(o);
        if (write !== false) {
          fs.mkdirSync(path.join(o.cwd, "graphify-out"), { recursive: true });
          const body = typeof write === "number" ? "x".repeat(write) : JSON.stringify({ nodes: [{ id: "a" }], links: [] });
          fs.writeFileSync(path.join(o.cwd, "graphify-out", "graph.json"), body);
        }
        return { code, stdout: "", stderr: code === 0 ? "" : "Traceback: boom\nmore" };
      },
    };
  }
  const make = (r: ProcessRunner, platform: NodeJS.Platform, has: (b: string) => boolean) =>
    makeGraphifyProvider({ bin: "graphify", version: "1.2.3", runner: r, platform, has, home: HOME });

  it("runs graphify sandboxed in the snapshot with a clean environment and parses its output", async () => {
    const r = runner(0, true);
    const p = make(r, "darwin", () => true);
    const snap = tempDir();
    expect((await p.build(snap)).nodes).toEqual([{ id: "a", file: null, name: null, line: null }]);
    const argv = r.argv[0];
    expect(argv.slice(0, 2)).toEqual(["sandbox-exec", "-p"]);
    expect(argv[2]).toContain(`(subpath "${fs.realpathSync(snap)}")`);
    expect(argv.slice(3)).toEqual(["graphify", "extract", snap, "--code-only", "--no-viz"]);
    expect(r.opts[0]).toMatchObject({ cwd: snap, cleanEnv: true });
    expect(p.version).toBe("1.2.3");
  });

  it("fails closed without a sandbox, without graphify, on a failed run, and on a missing or huge graph.json", async () => {
    await expect(make(runner(0, true), "linux", (b) => b === "graphify").build(tempDir())).rejects.toThrow(/SND-INDEX-007|no network sandbox/);
    await expect(make(runner(0, true), "darwin", (b) => b !== "graphify").build(tempDir())).rejects.toThrow("graphify is not installed (sindri index setup)");
    await expect(make(runner(1, false), "darwin", () => true).build(tempDir())).rejects.toThrow("graphify failed (exit 1): Traceback: boom");
    await expect(make(runner(0, false), "darwin", () => true).build(tempDir())).rejects.toThrow("graphify wrote no graph.json");
    await expect(make(runner(0, 33 * 1024 * 1024), "darwin", () => true).build(tempDir())).rejects.toThrow("graphify wrote a graph.json over 32 MB");
  });
});
