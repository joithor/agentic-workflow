import { parseFlags } from "../args.js";
import fs from "node:fs";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { requireApprovedProfile } from "../profile/approve.js";
import type { LoadedProfile } from "../profile/load.js";
import { buildIndex, type BuildReport } from "./build.js";
import { indexPath, LAYERS, layers, meta, openIndexReadOnly } from "./db.js";
import { makeOllamaEmbedder, type Embedder } from "./embed.js";
import type { IndexIo } from "./io.js";

// Read-only: index commands never create or migrate the ledger. A missing ledger is "nothing approved".
export function approvedOrThrow(deps: Deps): LoadedProfile {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) throw new SindriError("SND-PROFILE-012", "no approved profile", { fix: "sindri profile approve" });
  return readLedger(file, (db) => requireApprovedProfile(deps, db));
}

function reposOf(loaded: LoadedProfile, only: string | undefined): string[] {
  if (only === undefined) return Object.keys(loaded.repos).sort();
  if (!(only in loaded.repos)) throw new SindriError("SND-PROFILE-004", `no repo named ${only}`);
  return [only];
}

type LayerInfo = Record<string, { status: string; detail: string }>;

// A layer that is not ok or disabled says why, in text as well as in --json.
function describeLayers(ls: LayerInfo): string {
  return LAYERS.map((n) => {
    const l = ls[n] ?? { status: "pending", detail: "not built yet" };
    return l.status === "ok" || l.status === "disabled" ? `${n} ${l.status}` : `${n} ${l.status} (${l.detail})`;
  }).join(", ");
}

export function embedderFor(loaded: LoadedProfile, io: IndexIo): Embedder | null {
  const e = loaded.profile.index.embeddings;
  return e.enabled ? makeOllamaEmbedder({ url: e.url, model: e.model, fetch: io.fetch }) : null;
}

// The profile schema already refuses these; if one slips through, the layer reports
// unavailable with the reason and the other layers still build (no request is made).
export function embedderOrUnavailable(loaded: LoadedProfile, io: IndexIo): Embedder | null {
  try {
    return embedderFor(loaded, io);
  } catch (e) {
    return {
      model: loaded.profile.index.embeddings.model,
      embed: async () => {
        throw e;
      },
    };
  }
}

async function build(args: string[], deps: Deps, io: IndexIo): Promise<CommandResult> {
  const { values } = parseFlags(args, { repo: { type: "string" }, full: { type: "boolean" }, quick: { type: "boolean" }, json: { type: "boolean" } });
  const loaded = approvedOrThrow(deps);
  const quick = values.quick === true;
  const reports: BuildReport[] = [];
  for (const repo of reposOf(loaded, values.repo)) {
    deps.log(`building ${repo}${quick ? " (quick: structure, clones, deps)" : ""}; this takes the heavy-job lock`);
    reports.push(await buildIndex(deps, loaded, repo, { full: values.full === true, quick, mirror: !quick }, { embedder: embedderOrUnavailable(loaded, io), graph: null }));
  }
  const text = reports
    .map((r) => `${r.repo}: ${r.files.indexed} files (${r.files.changed} changed, ${r.files.removed} removed, ${r.files.skipped} skipped), ${r.symbols} symbols; ${describeLayers(r.layers)} (${(r.ms / 1000).toFixed(1)} s)`)
    .join("\n");
  return success(text, reports, values.json === true);
}

function age(ms: number): string {
  const h = Math.floor(ms / 3_600_000);
  return h >= 1 ? `${h} h` : `${Math.max(0, Math.floor(ms / 60_000))} min`;
}

interface StatusRow {
  repo: string;
  missing: boolean;
  commit: string;
  builtAt: string | null;
  ageMs: number | null;
  stale: boolean;
  layers: LayerInfo;
}

function statusLines(r: StatusRow): string[] {
  if (r.missing) return [`${r.repo}: no index (sindri index build --repo ${r.repo})`];
  const built = r.ageMs === null ? "never built" : `built ${age(r.ageMs)} ago at ${r.commit.slice(0, 12)}`;
  return [`${r.repo}: ${r.stale ? `stale (${built})` : built}; ${describeLayers(r.layers)}`, ...(r.stale ? [`  fix: sindri index build --repo ${r.repo}`] : [])];
}

// Lists every repo: a missing or stale index is a row and an exit code of 1, never an abort.
function status(args: string[], deps: Deps): CommandResult {
  const { values } = parseFlags(args, { repo: { type: "string" }, json: { type: "boolean" } });
  const loaded = approvedOrThrow(deps);
  const rows = reposOf(loaded, values.repo).map((repo): StatusRow => {
    const db = openIndexReadOnly(indexPath(deps, repo));
    if (db === null) return { repo, missing: true, commit: "", builtAt: null, ageMs: null, stale: true, layers: {} };
    const m = meta(db);
    const ls: LayerInfo = Object.fromEntries(layers(db).map((l) => [l.layer, { status: l.status, detail: l.detail }]));
    db.close();
    const ageMs = m.builtAt === null ? null : deps.now().getTime() - Date.parse(m.builtAt);
    return { repo, missing: false, commit: m.commit, builtAt: m.builtAt, ageMs, stale: ageMs === null || ageMs > loaded.profile.index.maxAgeHours * 3_600_000, layers: ls };
  });
  return success(rows.flatMap(statusLines).join("\n"), rows, values.json === true, rows.some((r) => r.stale) ? 1 : 0);
}

export function makeIndexCommand(io: IndexIo): Command {
  return async (args, deps) => {
    const [sub, ...rest] = args;
    const json = rest.includes("--json");
    try {
      if (sub === "build") return await build(rest, deps, io);
      if (sub === "status") return status(rest, deps);
      return failure("SND-CLI-002", `unknown index subcommand: ${sub ?? "(none)"}; use build, status, query or setup`, json, { fix: "sindri index --help" });
    } catch (e) {
      return fromError(e, json);
    }
  };
}
