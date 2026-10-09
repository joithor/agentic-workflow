import { parseFlags } from "../args.js";
import fs from "node:fs";

import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult, type ExitCode } from "../output.js";
import { requireApprovedProfile } from "../profile/approve.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { buildIndex, type BuildReport } from "./build.js";
import { allSymbols, bandCandidates, indexPath, LAYERS, layers, meta, openIndexReadOnly, symbolsByAstHash } from "./db.js";
import { makeOllamaEmbedder, type Embedder } from "./embed.js";
import { makeGraphifyProvider, type GraphProvider } from "./graph.js";
import type { IndexIo } from "./io.js";
import { bandKeys, estimateJaccard } from "./minhash.js";
import { GRAPHIFY_PIN } from "./pins.js";
import { runSetup, type Step } from "./setup.js";

// Read-only: index commands never create or migrate the ledger. A missing ledger is "nothing approved".
export function approvedOrThrow(deps: Deps): LoadedProfile {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) throw new SindriError("SND-PROFILE-012", "no approved profile", { fix: "sindri profile approve" });
  return readLedger(file, (db) => requireApprovedProfile(deps, db));
}

// Repos the live profile lists that the approved snapshot doesn't: `repo add` without
// `profile approve`. Read-only; a missing or invalid live profile lists nothing.
export function unapprovedRepos(deps: Deps, approved: LoadedProfile): string[] {
  const root = resolveProfileRoot(deps);
  const live = root === null ? null : loadProfile(root);
  return live !== null && live.ok ? Object.keys(live.value.repos).filter((n) => !Object.hasOwn(approved.repos, n)).sort() : [];
}

function reposOf(deps: Deps, loaded: LoadedProfile, only: string | undefined): string[] {
  if (only === undefined) return Object.keys(loaded.repos).sort();
  if (!(only in loaded.repos)) {
    if (unapprovedRepos(deps, loaded).includes(only)) {
      throw new SindriError("SND-PROFILE-015", `${only} is in the live profile but not approved yet`, {
        fix: "sindri profile approve (review the diff), then at a terminal: sindri profile approve <hash>; or sindri repo onboard, which prints both",
        exitCode: 1,
      });
    }
    throw new SindriError("SND-PROFILE-004", `no repo named ${only}`);
  }
  return [only];
}

// One line per step (index setup, repo onboard): status, name, detail, then the fix.
export function renderSteps(steps: Step[]): string {
  return steps.map((s) => `${s.status.padEnd(5)} ${s.name}  ${s.detail}${s.fix === undefined ? "" : `\n     fix: ${s.fix}`}`).join("\n");
}

type LayerInfo = Record<string, { status: string; detail: string }>;

// A layer that is not ok or disabled says why, in text as well as in --json.
function describeLayers(ls: LayerInfo): string {
  return LAYERS.map((n) => {
    const l = ls[n] ?? { status: "pending", detail: "not built yet" };
    return l.status === "ok" || l.status === "disabled" ? `${n} ${l.status}` : `${n} ${l.status} (${l.detail})`;
  }).join(", ");
}

export function embedderFor(loaded: LoadedProfile, io: Pick<IndexIo, "fetch">): Embedder | null {
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

export function graphFor(loaded: LoadedProfile, deps: Deps, io: IndexIo): GraphProvider | null {
  if (loaded.profile.index.graph === "none") return null;
  return makeGraphifyProvider({ bin: "graphify", version: GRAPHIFY_PIN, runner: { run: io.probes.run }, platform: deps.system.platform, has: io.probes.has, home: deps.home, runtimeDir: deps.env.XDG_RUNTIME_DIR, maxGraphMB: loaded.profile.index.graphMaxMB });
}

async function build(args: string[], deps: Deps, io: IndexIo): Promise<CommandResult> {
  const { values } = parseFlags(args, { repo: { type: "string" }, full: { type: "boolean" }, quick: { type: "boolean" }, json: { type: "boolean" } });
  const loaded = approvedOrThrow(deps);
  const quick = values.quick === true;
  const reports: BuildReport[] = [];
  const lines: string[] = [];
  for (const repo of reposOf(deps, loaded, values.repo)) {
    deps.log(`building ${repo}${quick ? " (quick: structure, clones, deps)" : ""}; this takes the heavy-job lock`);
    try {
      // The hourly quick build tries the lock once: waiting is pointless (the next hour retries)
      // and a nightly full build may hold it for long.
      const r = await buildIndex(deps, loaded, repo, { full: values.full === true, quick, mirror: !quick, lockTimeoutMs: quick ? 0 : undefined }, { embedder: embedderOrUnavailable(loaded, io), graph: graphFor(loaded, deps, io) });
      reports.push(r);
      lines.push(`${r.repo}: ${r.files.indexed} files (${r.files.changed} changed, ${r.files.removed} removed, ${r.files.skipped} skipped), ${r.symbols} symbols; ${describeLayers(r.layers)} (${(r.ms / 1000).toFixed(1)} s)`);
    } catch (e) {
      if (!quick || !(e instanceof SindriError) || e.code !== "SND-INDEX-001") throw e;
      lines.push(`${repo}: skipped (${e.message}); the next hourly run retries`);
    }
  }
  const pending = values.repo === undefined ? unapprovedRepos(deps, loaded) : [];
  if (pending.length > 0) lines.push(`skipped (in the live profile, not approved yet): ${pending.join(", ")}; run sindri profile approve`);
  const text = lines.join("\n");
  return success(text, { reports, unapproved: pending }, values.json === true);
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
  const rows = reposOf(deps, loaded, values.repo).map((repo): StatusRow => {
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

interface QueryRow {
  repo: string;
  at: string;
  name: string;
  relation: "match" | "exact" | "near";
  similarity: number;
}

// For humans at a terminal: names and paths are repo text, so this output is never fed to a session.
function query(args: string[], deps: Deps): CommandResult {
  const { values, positionals } = parseFlags(args, { repo: { type: "string" }, json: { type: "boolean" } });
  const name = positionals[0];
  if (name === undefined) throw new SindriError("SND-CLI-002", "index query needs a symbol name", { fix: "sindri index query <name> [--repo NAME]" });
  const loaded = approvedOrThrow(deps);
  const minJaccard = loaded.profile.shape.thresholds.nearCloneJaccard;
  const rows: QueryRow[] = [];
  const lines: string[] = [];
  for (const repo of reposOf(deps, loaded, values.repo)) {
    const db = openIndexReadOnly(indexPath(deps, repo));
    if (db === null) throw new SindriError("SND-INDEX-404", `no index for ${repo}`, { fix: `sindri index build --repo ${repo}` });
    const all = allSymbols(db);
    const found = all.filter((s) => s.name === name);
    for (const s of found) {
      const at = (x: { file: string; startLine: number }): string => `${x.file}:${x.startLine}`;
      rows.push({ repo, at: at(s), name: s.name, relation: "match", similarity: 1 });
      lines.push(`${at(s)} ${s.name}`);
      for (const e of symbolsByAstHash(db, s.astHash).filter((x) => x.id !== s.id)) {
        rows.push({ repo, at: at(e), name: e.name, relation: "exact", similarity: 1 });
        lines.push(`${at(e)} ${e.name} (exact)`);
      }
      const ids = new Set(bandCandidates(db, bandKeys(s.minhash)));
      const near = all
        .filter((c) => ids.has(c.id) && c.astHash !== s.astHash)
        .map((c) => ({ c, j: estimateJaccard(s.minhash, c.minhash) }))
        .filter(({ j }) => j >= minJaccard);
      for (const { c, j } of near) {
        rows.push({ repo, at: at(c), name: c.name, relation: "near", similarity: j });
        lines.push(`${at(c)} ${c.name} (near ${j.toFixed(2)})`);
      }
    }
    db.close();
    if (found.length === 0) lines.push(`No symbol named ${name} in ${repo}.`);
  }
  return success(lines.join("\n"), rows, values.json === true);
}

// Reads the APPROVED profile: an unapproved edit can't choose which model is pulled or which repos are indexed.
async function setup(args: string[], deps: Deps, io: IndexIo): Promise<CommandResult> {
  const { values } = parseFlags(args, { "dry-run": { type: "boolean" }, json: { type: "boolean" } });
  const approved = approvedOrThrow(deps);
  const { steps } = await runSetup(approved, io.probes, { dryRun: values["dry-run"] === true, platform: deps.system.platform, home: deps.home, runtimeDir: deps.env.XDG_RUNTIME_DIR, log: deps.log });
  const text = renderSteps(steps);
  const exit: ExitCode = steps.some((s) => s.status === "fail") ? 2 : steps.some((s) => s.status === "warn") ? 1 : 0;
  return success(text === "" ? "Nothing to set up: embeddings and the graph are off in the profile." : text, steps, values.json === true, exit);
}

export function makeIndexCommand(io: IndexIo): Command {
  return async (args, deps) => {
    const [sub, ...rest] = args;
    const json = rest.includes("--json");
    try {
      if (sub === "build") return await build(rest, deps, io);
      if (sub === "status") return status(rest, deps);
      if (sub === "query") return query(rest, deps);
      if (sub === "setup") return await setup(rest, deps, io);
      return failure("SND-CLI-002", `unknown index subcommand: ${sub ?? "(none)"}; use build, status, query or setup`, json, { fix: "sindri index --help" });
    } catch (e) {
      return fromError(e, json);
    }
  };
}
