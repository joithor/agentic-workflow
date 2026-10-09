import fs from "node:fs";
import path from "node:path";

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

const MAX_GRAPH_BYTES = 32 * 1024 * 1024;
const quote = (p: string): string => p.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
const subpath = (p: string): string => `(subpath "${quote(p)}")`;

// macOS: no network; no writes except the snapshot, the system temp dirs, /dev and ~/.cache;
// no reads of the credential dirs. Everything else stays readable (spec amendment 8: the
// residual risk is documented). In SBPL the last matching rule wins, so the allow follows the deny.
function macProfile(o: { writable: string[]; home: string }): string {
  const writes = [...o.writable.map(subpath), subpath("/private/var/folders"), subpath("/private/tmp"), subpath("/dev"), subpath(`${o.home}/.cache`)];
  const hidden = [".ssh", ".aws", ".gnupg", ".agentic-workflow", "Library/Keychains"].map((d) => subpath(`${o.home}/${d}`));
  return `(version 1)(allow default)(deny network*)(deny file-write*)(allow file-write* ${writes.join(" ")})(deny file-read* ${hidden.join(" ")})`;
}

// Spec §6.2: graphify runs with the network denied and the filesystem locked down, and fails
// closed without a sandbox.
export function sandboxArgv(platform: NodeJS.Platform, argv: string[], has: (bin: string) => boolean, o: { writable: string[]; home: string }): string[] | null {
  if (platform === "darwin" && has("sandbox-exec")) return ["sandbox-exec", "-p", macProfile(o), ...argv];
  if (platform === "linux" && has("bwrap")) {
    return [
      "bwrap", "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
      ...o.writable.flatMap((w) => ["--bind", w, w]),
      ...[".ssh", ".aws", ".gnupg", ".agentic-workflow"].flatMap((d) => ["--tmpfs", `${o.home}/${d}`]),
      "--unshare-net", "--unshare-pid", "--die-with-parent",
      ...argv,
    ];
  }
  return null;
}

type Obj = Record<string, unknown>;
const str = (o: Obj, keys: string[]): string | null => {
  for (const k of keys) if (typeof o[k] === "string" || typeof o[k] === "number") return String(o[k]);
  return null;
};

function lineOf(o: Obj): number | null {
  for (const k of ["line", "lineno", "source_line", "start_line"]) if (typeof o[k] === "number") return o[k] as number;
  const loc = str(o, ["source_location", "location"]);
  // "a.ts:12", or graphify's "L12".
  const m = loc === null ? null : /(?::|^L)(\d+)$/.exec(loc);
  return m === null ? null : Number(m[1]);
}

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
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) throw new SindriError("SND-INDEX-008", "graphify wrote an unexpected graph.json");
  const json = parsed as Obj;
  const nodes = (Array.isArray(json.nodes) ? json.nodes : []) as Obj[];
  const links = (Array.isArray(json.links) ? json.links : Array.isArray(json.edges) ? json.edges : []) as Obj[];
  return {
    nodes: nodes.map((n) => ({
      id: String(n.id),
      file: str(n, ["source_file", "file", "path", "filepath"]),
      name: str(n, ["label", "name", "qualname"]),
      line: lineOf(n),
    })),
    edges: links.map((l) => ({
      src: String(l.source),
      dst: String(l.target),
      relation: str(l, ["relation", "type", "label", "kind"]) ?? "related",
      confidence: str(l, ["confidence"]) ?? "UNKNOWN",
    })),
  };
}

const scrubber = makeScrubber();

export function makeGraphifyProvider(o: {
  bin: string;
  version: string;
  runner: ProcessRunner;
  platform: NodeJS.Platform;
  has: (bin: string) => boolean;
  home: string;
}): GraphProvider {
  return {
    version: o.version,
    async build(snapshotDir) {
      if (!o.has(o.bin)) throw new SindriError("SND-INDEX-008", `${o.bin} is not installed (sindri index setup)`);
      const argv = sandboxArgv(o.platform, [o.bin, "extract", snapshotDir, "--code-only", "--no-viz"], o.has, { writable: [fs.realpathSync(snapshotDir)], home: o.home });
      if (argv === null) {
        throw new SindriError("SND-INDEX-007", "no network sandbox available (sandbox-exec on macOS, bwrap on Linux); graphify never runs unsandboxed");
      }
      const r = await o.runner.run(argv, { cwd: snapshotDir, timeoutMs: 600_000, cleanEnv: true });
      if (r.code !== 0) {
        const first = scrubber.scrub(r.stderr.split("\n")[0]).text;
        throw new SindriError("SND-INDEX-008", `graphify failed (exit ${r.code}): ${first}`);
      }
      const out = path.join(snapshotDir, "graphify-out", "graph.json");
      if (!fs.existsSync(out)) throw new SindriError("SND-INDEX-008", "graphify wrote no graph.json");
      if (fs.statSync(out).size > MAX_GRAPH_BYTES) throw new SindriError("SND-INDEX-008", "graphify wrote a graph.json over 32 MB");
      return parseGraphJson(fs.readFileSync(out, "utf8"));
    },
  };
}
