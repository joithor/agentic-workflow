import fs from "node:fs";
import path from "node:path";

import { z } from "zod";

import { SindriError } from "../errors.js";
import { makeScrubber } from "../scrub/scrub.js";
import type { ProcessRunner } from "./io.js";

export interface GraphData {
  nodes: { id: string; file: string | null; name: string | null; line: number | null }[];
  edges: { src: string; dst: string; relation: string; confidence: string }[];
}

export interface GraphProvider {
  version: string;
  build(snapshotDir: string): Promise<GraphData>;
}

const quote = (p: string): string => p.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
const subpath = (p: string): string => `(subpath "${quote(p)}")`;

// The sandbox binaries by absolute path, never through PATH: a `sandbox-exec` or `bwrap` that a
// tool install dropped earlier on PATH would run graphify unsandboxed. `has` must answer true
// only for a root-owned file (the real one: sandbox-real.ts `hasBinary`).
export const SANDBOX_EXEC = "/usr/bin/sandbox-exec";
export const BWRAP = ["/usr/bin/bwrap", "/bin/bwrap"];

// graphify's private temp and cache dir, inside the snapshot (the only writable path): uv, pip
// and Python keep code in ~/.cache and the temp dirs that unsandboxed tools later run. Each run
// makes a fresh one with an unguessable name (mkdtemp of `.sindri-tmp-`), and the snapshot never
// holds tracked files under it or under graphify-out/, so a repo can't pre-seed either.
export const PRIVATE_DIR = ".sindri-tmp";

export function isReservedSnapshotPath(rel: string): boolean {
  const first = rel.split("/")[0];
  return first === "graphify-out" || first.startsWith(PRIVATE_DIR);
}
export const privateEnv = (dir: string): Record<string, string> => ({
  TMPDIR: dir,
  XDG_CACHE_HOME: `${dir}/cache`,
  UV_CACHE_DIR: `${dir}/cache/uv`,
  PYTHONPYCACHEPREFIX: `${dir}/pycache`,
});

// Credential and token stores under the home: unreadable inside the sandbox.
const SECRETS = [".ssh", ".aws", ".gnupg", ".agentic-workflow", ".config/gh", ".docker", ".kube", ".codex", ".claude", ".claude.json", ".netrc", ".git-credentials", ".npmrc", ".pypirc"];

// macOS: no network, no LaunchServices opens or Apple events (`open URL` would start a browser
// outside the sandbox, so the network deny wouldn't apply to it), and no launchctl (a launchd
// job runs outside the sandbox; launchd also refuses a sandboxed submit); no writes except the
// snapshot and /dev; no reads of the credential stores. Everything else stays readable (spec
// amendment 8: the residual risk is documented). In SBPL the last matching rule wins, so the
// allow follows the deny. SBPL matches resolved paths, so each store is denied by its own real
// path as well as by name: under a symlinked home or a symlinked ~/.ssh the name matches nothing.
const LAUNCH_SERVICES = '(deny lsopen)(deny appleevent-send)(deny mach-lookup (global-name "com.apple.coreservices.launchservicesd"))';
const LAUNCHCTL = '(deny process-exec (literal "/bin/launchctl"))';

function realOr(p: string): string {
  try {
    return fs.realpathSync(p);
  } catch {
    return p;
  }
}

function macProfile(o: { writable: string[]; home: string }): string {
  const home = realOr(o.home);
  const writes = [...o.writable.map(subpath), subpath("/dev")];
  const hidden = [...new Set([...SECRETS, "Library/Keychains"].flatMap((d) => [`${home}/${d}`, realOr(`${home}/${d}`)]))].map(subpath);
  return `(version 1)(allow default)(deny network*)${LAUNCH_SERVICES}${LAUNCHCTL}(deny file-write*)(allow file-write* ${writes.join(" ")})(deny file-read* ${hidden.join(" ")})`;
}

export type PathKind = "dir" | "file" | null;

// What a path is, following symlinks; null when it is missing.
export function pathKind(p: string): PathKind {
  try {
    return fs.statSync(p).isDirectory() ? "dir" : "file";
  } catch {
    return null;
  }
}

// Linux: an empty tmpfs over each credential dir and /dev/null over each credential file, at
// their real paths (bwrap can't mount over a missing path on the read-only root, and a missing
// one holds no secrets). /run, /var/run and the runtime dir get a tmpfs too: a read-only bind
// still lets connect() reach the session D-Bus or docker.sock, each a way out.
function linuxMasks(o: SandboxOpts): string[] {
  const seen = new Set<string>();
  const mask = (p: string): string[] => {
    const kind = o.kind(p);
    const real = (o.realpath ?? realOr)(p);
    if (kind === null || seen.has(real)) return [];
    seen.add(real);
    return kind === "dir" ? ["--tmpfs", real] : ["--ro-bind", "/dev/null", real];
  };
  const runtimeDir = o.runtimeDir === undefined || o.runtimeDir.startsWith("/run/") ? [] : [o.runtimeDir];
  const runtime = ["/run", "/var/run", ...runtimeDir].flatMap(mask);
  return [...runtime, ...SECRETS.map((d) => `${o.home}/${d}`).flatMap(mask)];
}

// `kind` and `realpath` (default: the real one) are used on Linux only: macOS denies by path
// whether or not it exists. `runtimeDir`: $XDG_RUNTIME_DIR.
export interface SandboxOpts {
  writable: string[];
  home: string;
  kind: (p: string) => PathKind;
  realpath?: (p: string) => string;
  runtimeDir?: string;
}

// Spec §6.2: graphify runs with the network denied and writes confined to the snapshot, and
// fails closed without a sandbox.
export function sandboxArgv(platform: NodeJS.Platform, argv: string[], has: (bin: string) => boolean, o: SandboxOpts): string[] | null {
  if (platform === "darwin" && has(SANDBOX_EXEC)) return [SANDBOX_EXEC, "-p", macProfile(o), ...argv];
  const bwrap = platform === "linux" ? BWRAP.find(has) : undefined;
  if (bwrap === undefined) return null;
  return [
    bwrap, "--unshare-all", "--new-session", "--die-with-parent",
    "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
    ...linuxMasks(o),
    ...o.writable.flatMap((w) => ["--bind", w, w]),
    ...argv,
  ];
}

type Obj = Record<string, unknown>;
const str = (o: Obj, keys: string[]): string | null => {
  for (const k of keys) if (typeof o[k] === "string" || typeof o[k] === "number") return String(o[k]);
  return null;
};

function lineOf(o: Obj): number | null {
  for (const k of ["line", "lineno", "source_line", "start_line"]) {
    const v = o[k];
    if (typeof v === "number") return v;
  }
  const loc = str(o, ["source_location", "location"]);
  // "a.ts:12", or graphify's "L12".
  const m = loc === null ? null : /(?::|^L)(\d+)$/.exec(loc);
  return m === null ? null : Number(m[1]);
}

// A node id or an edge endpoint: a string or a number, never a coerced "undefined".
const Id = z.union([z.string(), z.number()]).transform(String);
const Node = z.object({ id: Id }).passthrough();
const Link = z.object({ source: Id, target: Id }).passthrough();
const Graph = z.object({ nodes: z.array(Node).default([]), links: z.array(Link).optional(), edges: z.array(Link).optional() }).passthrough();

// NetworkX node-link JSON. Field names come from candidate lists because
// graphify's schema is undocumented; Task 7 Step 1 records a real fixture.
// graph.json is untrusted output of a third-party tool: anything unexpected is an error.
export function parseGraphJson(text: string): GraphData {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    throw new SindriError("SND-INDEX-008", "graphify wrote invalid JSON");
  }
  const g = Graph.safeParse(parsed);
  if (!g.success) throw new SindriError("SND-INDEX-008", "graphify wrote an unexpected graph.json");
  return {
    nodes: g.data.nodes.map((n) => ({
      id: n.id,
      file: str(n, ["source_file", "file", "path", "filepath"]),
      name: str(n, ["label", "name", "qualname"]),
      line: lineOf(n),
    })),
    edges: (g.data.links ?? g.data.edges ?? []).map((l) => ({
      src: l.source,
      dst: l.target,
      relation: str(l, ["relation", "type", "label", "kind"]) ?? "related",
      confidence: str(l, ["confidence"]) ?? "UNKNOWN",
    })),
  };
}

// graphify writes absolute paths into the temp snapshot, which is deleted after the build:
// store them relative to the snapshot root; a path outside it, in the private dir or in
// graphify-out/, is null.
function inSnapshot(file: string, roots: string[]): string | null {
  let rel = file;
  for (const root of roots) {
    if (file.startsWith(`${root}/`)) {
      rel = file.slice(root.length);
      break;
    }
  }
  if (rel === file && path.isAbsolute(file)) return null;
  const norm = path.posix.normalize(rel.replace(/^\/+/, ""));
  return norm === "." || norm === ".." || norm.startsWith("../") || isReservedSnapshotPath(norm) ? null : norm;
}

// One message for the throw and one pattern for doctor, so they cannot drift apart.
export const graphOverCap = (maxMB: number): string => `graphify wrote a graph.json over ${maxMB} MB (raise index.graphMaxMB, max 512)`;
export const GRAPH_OVER_CAP = /^graphify wrote a graph\.json over \d+ MB \(raise index\.graphMaxMB/;

const unusable = (): SindriError => new SindriError("SND-INDEX-008", "graphify wrote an unusable graph.json");

// graph.json comes from the sandboxed process and is read unsandboxed: never through a
// symlink (it could point at a file the sandbox hides), and only a regular file (a FIFO would
// block forever, /dev/zero would read without end). O_NONBLOCK: opening a FIFO doesn't wait.
function readGraphFile(snapshotDir: string, maxMB: number): string {
  const dir = path.join(snapshotDir, "graphify-out");
  let st: fs.Stats;
  try {
    st = fs.lstatSync(dir);
  } catch {
    throw new SindriError("SND-INDEX-008", "graphify wrote no graph.json");
  }
  if (!st.isDirectory()) throw unusable();
  let fd: number;
  try {
    fd = fs.openSync(path.join(dir, "graph.json"), fs.constants.O_RDONLY | fs.constants.O_NOFOLLOW | fs.constants.O_NONBLOCK);
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code === "ENOENT") throw new SindriError("SND-INDEX-008", "graphify wrote no graph.json");
    throw unusable();
  }
  try {
    const f = fs.fstatSync(fd);
    if (!f.isFile()) throw unusable();
    if (f.size > maxMB * 1024 * 1024) throw new SindriError("SND-INDEX-008", graphOverCap(maxMB));
    return fs.readFileSync(fd, "utf8");
  } finally {
    fs.closeSync(fd);
  }
}

const scrubber = makeScrubber();

export function makeGraphifyProvider(o: {
  bin: string;
  version: string;
  runner: ProcessRunner;
  platform: NodeJS.Platform;
  has: (bin: string) => boolean;
  home: string;
  runtimeDir?: string;
  maxGraphMB: number;
}): GraphProvider {
  return {
    version: o.version,
    async build(snapshotDir) {
      if (!o.has(o.bin)) throw new SindriError("SND-INDEX-008", `${o.bin} is not installed (sindri index setup)`);
      const real = fs.realpathSync(snapshotDir);
      const box = { writable: [real], home: o.home, kind: pathKind, runtimeDir: o.runtimeDir };
      const argv = sandboxArgv(o.platform, [o.bin, "extract", snapshotDir, "--code-only", "--no-viz"], o.has, box);
      if (argv === null) {
        throw new SindriError("SND-INDEX-007", "no network sandbox available (sandbox-exec on macOS, bwrap on Linux); graphify never runs unsandboxed");
      }
      const priv = fs.mkdtempSync(path.join(real, `${PRIVATE_DIR}-`));
      fs.mkdirSync(path.join(priv, "cache"), { mode: 0o700 });
      const r = await o.runner.run(argv, { cwd: snapshotDir, timeoutMs: 600_000, cleanEnv: true, env: privateEnv(priv) });
      if (r.code !== 0) {
        const first = scrubber.scrub(r.stderr.split("\n")[0]).text;
        throw new SindriError("SND-INDEX-008", `graphify failed (exit ${r.code}): ${first}`);
      }
      const g = parseGraphJson(readGraphFile(snapshotDir, o.maxGraphMB));
      return { ...g, nodes: g.nodes.map((n) => ({ ...n, file: n.file === null ? null : inSnapshot(n.file, [snapshotDir, real]) })) };
    },
  };
}
