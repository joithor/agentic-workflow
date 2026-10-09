import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import type { Ledger } from "../ledger/db.js";
import { makeScrubber } from "../scrub/scrub.js";
import { LAYERS } from "./db.js";
import { SIGNAL_TYPES, type Signal } from "./signals.js";

export interface ShapeRun {
  runId: string;
  repo: string;
  ts: string;
  head: string | null;
  tree: string | null;
  elapsedMs: number;
  indexAgeMs: number | null;
  providers: { embedder: string | null; graph: string | null };
  // parserId() of the hook that computed the signals' AST hashes.
  parser: string;
  deferred: string[];
  signals: Signal[];
}

const MAX_BYTES = 1024 * 1024;
const HEX40 = /^[0-9a-f]{40}$/;
const CONTROL = /[\u0000-\u001f\u007f]/g;
const RunSchema = z
  .object({
    runId: z.string().regex(/^[0-9a-z]{26}$/),
    repo: z.string().regex(/^[a-z0-9][a-z0-9-]{0,38}$/),
    ts: z.string().datetime(),
    head: z.string().regex(HEX40).nullable(),
    tree: z.string().regex(HEX40).nullable(),
    elapsedMs: z.number().int().nonnegative(),
    indexAgeMs: z.number().int().nullable(),
    providers: z.object({ embedder: z.string().max(80).nullable(), graph: z.string().max(80).nullable() }).strict(),
    parser: z.string().max(80),
    deferred: z.array(z.string().max(32)).max(8),
    signals: z
      .array(
        z
          .object({
            type: z.enum(SIGNAL_TYPES),
            layer: z.enum(LAYERS),
            value: z.number(),
            threshold: z.number(),
            at: z.string().max(512),
            existing: z.string().max(512).nullable(),
            detail: z.string().max(2000),
            name: z.string().max(200).nullable(),
            astHash: z.string().regex(/^[0-9a-f]{64}$/).nullable(),
          })
          .strict(),
      )
      .max(500),
  })
  .strict();

export const spoolDir = (deps: Deps): string => path.join(stateDir(deps), "spool");

// Hook side: one file per run, written under a temp name and renamed into place.
export function writeShapeRun(deps: Deps, run: ShapeRun): string {
  const dir = spoolDir(deps);
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  const file = path.join(dir, `shape-${run.runId}.json`);
  fs.writeFileSync(`${file}.tmp`, JSON.stringify(run), { mode: 0o600 });
  fs.renameSync(`${file}.tmp`, file);
  return file;
}

// Spool files are untrusted data (spec §8.2): O_NOFOLLOW and an fstat on the open descriptor,
// so a file swapped for a link between the check and the read can't be followed.
function readRun(file: string): z.infer<typeof RunSchema> | null {
  let text: string | null = null;
  try {
    const fd = fs.openSync(file, fs.constants.O_RDONLY | fs.constants.O_NOFOLLOW);
    try {
      const st = fs.fstatSync(fd);
      if (st.isFile() && st.size <= MAX_BYTES) text = fs.readFileSync(fd, "utf8");
    } finally {
      fs.closeSync(fd);
    }
  } catch {
    return null;
  }
  if (text === null) return null;
  try {
    const r = RunSchema.safeParse(JSON.parse(text));
    // The file is named for its run, so pruneSpool can find what the ledger already holds.
    return r.success && path.basename(file) === `shape-${r.data.runId}.json` ? r.data : null;
  } catch {
    return null;
  }
}

// Sindri side, inside the tick lock. Inserts only: the files stay until pruneSpool runs after
// the enclosing transaction commits, so a rolled-back ingest loses nothing.
export function ingestSpool(db: Ledger, deps: Deps, epoch: number): { runs: number; signals: number; quarantined: number } {
  const dir = spoolDir(deps);
  const counts = { runs: 0, signals: 0, quarantined: 0 };
  if (!fs.existsSync(dir)) return counts;
  const scrubber = makeScrubber();
  const clean = (s: string): string => scrubber.scrub(s.replace(CONTROL, " ")).text;
  const quarantine = (name: string): void => {
    fs.mkdirSync(path.join(dir, "quarantine"), { recursive: true, mode: 0o700 });
    fs.renameSync(path.join(dir, name), path.join(dir, "quarantine", name));
    counts.quarantined++;
  };
  for (const name of fs.readdirSync(dir).filter((n) => n.startsWith("shape-") && n.endsWith(".json")).sort()) {
    const run = readRun(path.join(dir, name));
    if (run === null) {
      quarantine(name);
      continue;
    }
    // INSERT OR IGNORE on the run id: a replayed file inserts nothing, signals included.
    const inserted = db.transaction((): boolean => {
      const res = db
        .prepare("INSERT OR IGNORE INTO shape_runs (run_id, repo, ts, head, tree, commit_sha, elapsed_ms, index_age_ms, providers, parser, deferred, signal_count, epoch) VALUES (?, ?, ?, ?, ?, NULL, ?, ?, ?, ?, ?, ?, ?)")
        .run(run.runId, run.repo, run.ts, run.head, run.tree, run.elapsedMs, run.indexAgeMs, JSON.stringify(run.providers), clean(run.parser), JSON.stringify(run.deferred), run.signals.length, epoch);
      if (res.changes === 0) return false;
      for (const s of run.signals) {
        db.prepare("INSERT INTO shape_signals (run_id, type, layer, value, threshold, at, existing, detail, name, ast_hash, epoch) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)").run(
          run.runId, s.type, s.layer, s.value, s.threshold, clean(s.at), s.existing === null ? null : clean(s.existing), clean(s.detail), s.name === null ? null : clean(s.name), s.astHash, epoch,
        );
      }
      return true;
    })();
    if (inserted) {
      counts.runs++;
      counts.signals += run.signals.length;
    }
  }
  return counts;
}

// After the ingest committed: removes each spool file whose run the ledger now holds (a replay
// included). A file whose run isn't there (a rolled-back ingest) stays for the next one.
export function pruneSpool(db: Ledger, deps: Deps): void {
  const dir = spoolDir(deps);
  if (!fs.existsSync(dir)) return;
  const has = db.prepare("SELECT 1 FROM shape_runs WHERE run_id = ?");
  for (const name of fs.readdirSync(dir)) {
    const m = /^shape-([0-9a-z]{26})\.json$/.exec(name);
    if (m !== null && has.get(m[1]) !== undefined) fs.rmSync(path.join(dir, name));
  }
}
