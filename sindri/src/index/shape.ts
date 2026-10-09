import fs from "node:fs";
import path from "node:path";

import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import { ledgerPath, openLedger, openLedgerReadOnly, withEpoch } from "../ledger/db.js";
import { acquireTickLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { failure, fromError, success, type CommandResult } from "../output.js";
import { approvedProfile } from "../profile/approve.js";
import type { LoadedProfile } from "../profile/load.js";
import { SIZES, type Size } from "../profile/schema.js";
import { makeScrubber } from "../scrub/scrub.js";
import { embedderFor } from "./commands.js";
import { indexPath, layers, meta, openIndexReadOnly, type Layer } from "./db.js";
import type { IndexIo } from "./io.js";
import { buildOverlay, stagedChanges } from "./overlay.js";
import { computeSignals } from "./signals.js";
import { ingestSpool, pruneSpool, writeShapeRun } from "./spool.js";

async function commonDir(deps: Deps, cwd: string, foreign: boolean): Promise<string> {
  const r = await deps.git.run(["rev-parse", "--git-common-dir"], cwd, { foreign });
  return r.ok ? fs.realpathSync(path.resolve(cwd, r.stdout.trim())) : "";
}

// The commit's repo is the profile repo with the same git common dir: a linked worktree shares
// it with the main checkout, a clone doesn't. Git exports GIT_DIR to a hook in a linked worktree,
// so the profile repos are probed as foreign repos, or each would answer with the commit's own.
async function repoFor(deps: Deps, loaded: LoadedProfile, worktree: string): Promise<string | undefined> {
  const mine = await commonDir(deps, worktree, false);
  for (const [name, r] of Object.entries(loaded.repos)) {
    const other = await commonDir(deps, r.path, true);
    if (other !== "" && other === mine) return name;
  }
  return undefined;
}

type Recorded = { written: string | null; note: string };

// Record-only (spec amendment 5): every path returns normally; the hook never blocks, and
// never writes the ledger (it opens it read-only, with no migration).
export async function recordStaged(deps: Deps, io: Pick<IndexIo, "fetch">, o: { repo?: string; size?: Size }): Promise<Recorded> {
  const started = deps.now().getTime();
  const ledger = openLedgerReadOnly(ledgerPath(stateDir(deps)));
  if (ledger === null) return { written: null, note: "skipped (no approved profile)" };
  let loaded: LoadedProfile | null;
  try {
    loaded = approvedProfile(deps, ledger);
  } finally {
    ledger.close();
  }
  if (loaded === null) return { written: null, note: "skipped (no approved profile)" };
  if (!loaded.profile.shape.record) return { written: null, note: "" };
  // shape.budgetMs bounds the whole run: git, the overlay and the signals have no deadline of
  // their own. AbortSignal.timeout's timer never keeps the process alive, so a fast run exits at
  // once. A run that misses the budget writes nothing, even if it finishes later.
  const budgetMs = loaded.profile.shape.budgetMs;
  const over: Recorded = { written: null, note: `skipped (over the ${budgetMs} ms budget; nothing recorded)` };
  const budget = AbortSignal.timeout(Math.max(0, started + budgetMs - deps.now().getTime()));
  const timedOut = new Promise<Recorded>((resolve) => budget.addEventListener("abort", () => resolve(over), { once: true }));
  // The clock check catches work that held the thread, so the timer couldn't fire in time.
  const late = (): boolean => budget.aborted || deps.now().getTime() - started > budgetMs;
  return Promise.race([measure(deps, io, loaded, o, started, () => (late() ? over : null)), timedOut]);
}

async function measure(
  deps: Deps, io: Pick<IndexIo, "fetch">, loaded: LoadedProfile, o: { repo?: string; size?: Size }, started: number, overBudget: () => Recorded | null,
): Promise<Recorded> {
  const top = await deps.git.run(["rev-parse", "--show-toplevel"], deps.cwd);
  if (!top.ok) return { written: null, note: "skipped (not inside a git repo)" };
  const worktree = top.stdout.trim();
  const repo = o.repo ?? (await repoFor(deps, loaded, worktree));
  if (repo === undefined) return { written: null, note: "skipped (this repo is not in the profile)" };
  const cfg = loaded.repos[repo];
  if (cfg === undefined) return { written: null, note: `skipped (no repo named ${repo} in the profile)` };
  const base = openIndexReadOnly(indexPath(deps, repo));
  if (base === null) return { written: null, note: "skipped (no index; sindri index build)" };
  try {
    const ix = loaded.profile.index;
    const shape = loaded.profile.shape;
    const now = (): number => deps.now().getTime();
    // Every step after this one starts only inside the budget; parsing stops at it too.
    const { changes, addedLines, skipped, renames } = await stagedChanges(deps.git, worktree, { denyPaths: [...ix.denyPaths, ...cfg.index.denyPaths], maxFileKB: ix.maxFileKB });
    const stop1 = overBudget();
    if (stop1 !== null) return stop1;
    const overlay = buildOverlay(changes, addedLines, renames, { at: started + shape.budgetMs, now });
    const stop2 = overBudget();
    if (stop2 !== null) return stop2;
    if (overlay.symbols.length === 0 && overlay.manifests.length === 0) return { written: null, note: "" };
    const size = o.size ?? shape.defaultSize;
    const embedder = embedderFor(loaded, io);
    const { signals, deferred } = await computeSignals({
      base, overlay, t: shape.thresholds, sizeBudget: shape.sizeBudget[size], exportAllowance: shape.exportAllowance[size],
      embed: embedder === null ? null : { embedder, deadline: started + shape.budgetMs, now },
    });
    const stop3 = overBudget();
    if (stop3 !== null) return stop3;
    const head = await deps.git.run(["rev-parse", "HEAD"], worktree);
    const tree = await deps.git.run(["write-tree"], worktree);
    const built = meta(base).builtAt;
    const stamp = (name: Layer): string | null => {
      const l = layers(base).find((x) => x.layer === name && x.status === "ok");
      return l === undefined ? null : l.stamp;
    };
    const stop4 = overBudget();
    if (stop4 !== null) return stop4;
    const written = writeShapeRun(deps, {
      runId: ulid(deps.now()),
      repo,
      ts: deps.now().toISOString(),
      head: head.ok ? head.stdout.trim() : null,
      tree: tree.ok ? tree.stdout.trim() : null,
      elapsedMs: deps.now().getTime() - started,
      indexAgeMs: built === null ? null : started - Date.parse(built),
      providers: { embedder: stamp("embeddings"), graph: stamp("graph") },
      deferred,
      signals,
    });
    const types = [...new Set(signals.map((s) => s.type))].join(", ");
    const skippedNote = skipped.length === 0 ? "" : `; skipped ${skipped.length} staged file(s) (denied or over index.maxFileKB)`;
    return {
      written,
      note: signals.length === 0 ? "" : `${signals.length} signal(s) recorded (${types})${skippedNote}; record-only, the commit proceeds. See: sindri shape report --recent ${signals.length}`,
    };
  } finally {
    base.close();
  }
}

function table(head: string[], rows: string[][]): string[] {
  const widths = head.map((h, i) => Math.max(h.length, ...rows.map((r) => r[i].length)));
  const fmt = (c: string[]): string => c.map((v, i) => (i === c.length - 1 ? v : v.padEnd(widths[i]))).join("  ");
  return [fmt(head), ...rows.map(fmt)];
}

interface RecentRow {
  type: string;
  at: string;
  existing: string | null;
  value: number;
  threshold: number;
  detail: string;
  outcome: string | null;
  index_age_ms: number | null;
}

function evidence(r: RecentRow): string[] {
  const age = r.index_age_ms === null ? "index age unknown" : `index ${Math.floor(r.index_age_ms / 3_600_000)} h old`;
  return [`${r.type}  ${r.at} vs ${r.existing ?? "-"}  value ${r.value}/${r.threshold}  ${age}  ${r.outcome ?? "unlabeled"}`, `    ${r.detail}`];
}

async function report(args: string[], deps: Deps): Promise<CommandResult> {
  const { values } = parseFlags(args, { json: { type: "boolean" }, recent: { type: "string" } });
  const recentN = values.recent === undefined ? null : Number(values.recent);
  if (recentN !== null && !(Number.isInteger(recentN) && recentN > 0)) {
    throw new SindriError("SND-CLI-002", "--recent needs a positive whole number", { fix: "sindri shape report --recent 10" });
  }
  const db = openLedger(ledgerPath(stateDir(deps)));
  try {
    const lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
    let ingested = { runs: 0, signals: 0, quarantined: 0 };
    if (lock.ok) {
      try {
        ingested = withEpoch(db, lock.owner.epoch, () => ingestSpool(db, deps, lock.owner.epoch));
        pruneSpool(db, deps);
      } finally {
        lock.release();
      }
    }
    const rows = db.prepare("SELECT type, layer, COUNT(*) AS n FROM shape_signals GROUP BY type, layer ORDER BY n DESC, type").all() as { type: string; layer: string; n: number }[];
    const byType = Object.fromEntries(rows.map((r) => [r.type, r.n]));
    const recent =
      recentN === null
        ? []
        : (db
            .prepare("SELECT s.type, s.at, s.existing, s.value, s.threshold, s.detail, s.outcome, r.index_age_ms FROM shape_signals s JOIN shape_runs r ON r.run_id = s.run_id ORDER BY s.seq DESC LIMIT ?")
            .all(recentN) as RecentRow[]);
    const lines = [
      `Ingested ${ingested.runs} run(s), ${ingested.signals} signal(s).${ingested.quarantined > 0 ? ` Quarantined ${ingested.quarantined} bad spool file(s).` : ""}${lock.ok ? "" : " (Another run holds the lock; showing what's already ingested.)"}`,
      ...(rows.length === 0 ? ["No shape signals recorded yet."] : table(["TYPE", "SIGNALS", "LAYER"], rows.map((r) => [r.type, String(r.n), r.layer]))),
      ...(recent.length === 0 ? [] : ["", "Recent signals:", ...recent.flatMap(evidence)]),
    ];
    return success(lines.join("\n"), { ingested, byType, rows, recent }, values.json === true);
  } finally {
    db.close();
  }
}

export function makeShapeCommand(io: Pick<IndexIo, "fetch">): Command {
  return async (args, deps) => {
    const json = args.includes("--json");
    if (args[0] === "report") {
      try {
        return await report(args.slice(1), deps);
      } catch (e) {
        return fromError(e, json);
      }
    }
    if (!args.includes("--record")) return failure("SND-CLI-002", `unknown shape subcommand: ${args[0] ?? "(none)"}; use --record --staged or report`, json, { fix: "sindri shape --help" });
    try {
      const { values } = parseFlags(args, { record: { type: "boolean" }, staged: { type: "boolean" }, repo: { type: "string" }, size: { type: "string" } });
      const size = SIZES.find((s) => s === values.size);
      const r = await recordStaged(deps, io, { repo: values.repo, size });
      return { exitCode: 0, stdout: "", stderr: r.note === "" ? "" : `sindri-shape: ${r.note}\n` };
    } catch (e) {
      // Record-only: a failure is reported, never blocks the commit. Built-in scrub patterns
      // only (the profile may be what broke), one line.
      const message = makeScrubber().scrub(e instanceof Error ? e.message : String(e)).text.split("\n")[0];
      return { exitCode: 0, stdout: "", stderr: `sindri-shape: skipped (${message})\n` };
    }
  };
}
