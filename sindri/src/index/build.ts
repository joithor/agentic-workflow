import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import type { LoadedProfile } from "../profile/load.js";
import { compileExtraPatterns, makeScrubber, type Scrubber } from "../scrub/scrub.js";
import { type IndexDb, indexPath, type Layer, type LayerStatus, openIndex } from "./db.js";
import { embeddingText, encodeVec, type Embedder } from "./embed.js";
import { readManifestDeps } from "./deps-layer.js";
import { inventory, isSourcePath, type IndexedFile } from "./files.js";
import { matchesAny } from "./globs.js";
import { withHeavyLock } from "./heavy-lock.js";
import { refreshMirror } from "./mirror.js";
import { bandKeys, encodeSig, SHINGLE, signature } from "./minhash.js";
import { typescriptParser } from "./parse-ts.js";

// Bump when parsing or hashing changes: every structure/clone row is rebuilt.
export const INDEXER_VERSION = "1";
// The stamp includes the utility globs: they decide each symbol's `utility` flag, so
// changing them must re-parse unchanged files.
// It also includes scrub.extraPatterns: they decide what stored bodies redact, so a new pattern
// must reach the bodies of unchanged files.
const structureStamp = (utilityGlobs: readonly string[], extraPatterns: readonly { kind: string; regex: string }[]): string =>
  `parse-ts@${INDEXER_VERSION}+${createHash("sha256").update(JSON.stringify([utilityGlobs, extraPatterns])).digest("hex").slice(0, 8)}`;

// Replaced by the real interface in Task 7 (GraphProvider).
export type { Embedder } from "./embed.js";
export type GraphProvider = never;
export interface Providers {
  embedder: Embedder | null;
  graph: GraphProvider | null;
}

export interface BuildReport {
  repo: string;
  commit: string | null;
  quick: boolean;
  files: { indexed: number; changed: number; removed: number; skipped: number };
  symbols: number;
  layers: Record<Layer, { status: LayerStatus; detail: string }>;
  ms: number;
}

function setLayer(db: IndexDb, layer: Layer, stamp: string, status: LayerStatus, detail: string, now: Date): void {
  db.prepare(
    "INSERT INTO layers (layer, stamp, status, detail, built_at) VALUES (?, ?, ?, ?, ?) ON CONFLICT(layer) DO UPDATE SET stamp = excluded.stamp, status = excluded.status, detail = excluded.detail, built_at = excluded.built_at",
  ).run(layer, stamp, status, detail, now.toISOString());
}

function stampOf(db: IndexDb, layer: Layer): string | null {
  return (db.prepare("SELECT stamp FROM layers WHERE layer = ?").get(layer) as { stamp: string } | undefined)?.stamp ?? null;
}

// A killed or out-of-disk build leaves `<repo>.db.tmp-*`; the heavy-job lock guarantees
// no other build is running, so anything older than an hour is garbage.
function sweepTmp(deps: Deps, live: string): void {
  const dir = path.dirname(live);
  for (const name of fs.readdirSync(dir)) {
    if (!name.startsWith(`${path.basename(live)}.tmp-`)) continue;
    const file = path.join(dir, name);
    if (deps.now().getTime() - fs.statSync(file).mtimeMs > 3_600_000) fs.rmSync(file, { force: true });
  }
}

function writeStructure(db: IndexDb, files: IndexedFile[], utilityGlobs: readonly string[], stamp: string, scrubber: Scrubber): { changed: number; removed: number } {
  if (stampOf(db, "structure") !== stamp) db.exec("DELETE FROM files");
  const known = new Map((db.prepare("SELECT path, hash FROM files").all() as { path: string; hash: string }[]).map((r) => [r.path, r.hash]));
  const sources = files.filter((f) => isSourcePath(f.path));
  const live = new Set(sources.map((f) => f.path));
  const removed = [...known.keys()].filter((p) => !live.has(p));
  const changed = sources.filter((f) => known.get(f.path) !== f.hash);
  const insertSymbol = db.prepare(
    `INSERT INTO symbols (file, name, kind, start_line, end_line, exported, utility, signature, ast_hash, token_count, complexity, callees, minhash, body)
     VALUES (@file, @name, @kind, @startLine, @endLine, @exported, @utility, @signature, @astHash, @tokenCount, @complexity, @callees, @minhash, @body)`,
  );
  const insertBand = db.prepare("INSERT INTO bands (key, symbol_id) VALUES (?, ?)");
  db.transaction(() => {
    for (const p of removed) db.prepare("DELETE FROM files WHERE path = ?").run(p);
    for (const f of changed) {
      db.prepare("DELETE FROM files WHERE path = ?").run(f.path);
      db.prepare("INSERT INTO files (path, hash, size) VALUES (?, ?, ?)").run(f.path, f.hash, f.size);
      for (const s of typescriptParser.parse(f.path, f.text)) {
        const sig = signature(s.tokens);
        const { lastInsertRowid } = insertSymbol.run({
          ...s, exported: s.exported ? 1 : 0, utility: matchesAny(f.path, utilityGlobs) ? 1 : 0, tokenCount: s.tokens.length,
          callees: JSON.stringify(s.callees), minhash: encodeSig(sig), body: scrubber.scrub(s.text).text,
        });
        // Fewer tokens than one shingle: every such symbol would share bands with every other, so none is banded.
        if (s.tokens.length >= SHINGLE) for (const key of bandKeys(sig)) insertBand.run(key, Number(lastInsertRowid));
      }
    }
  })();
  return { changed: changed.length, removed: removed.length };
}

function writeDeps(db: IndexDb, files: IndexedFile[]): void {
  const rows = files.filter((f) => f.path === "package.json" || f.path.endsWith("/package.json")).flatMap((f) => readManifestDeps(f.path, f.text));
  db.transaction(() => {
    db.exec("DELETE FROM deps");
    for (const r of rows) db.prepare("INSERT OR REPLACE INTO deps (manifest, name, version, kind, tags) VALUES (?, ?, ?, ?, ?)").run(r.manifest, r.name, r.version, r.kind, JSON.stringify(r.tags));
  })();
}

async function embedLayer(db: IndexDb, embedder: Embedder | null, now: Date): Promise<void> {
  if (embedder === null) {
    setLayer(db, "embeddings", "none", "disabled", "no embedder configured", now);
    return;
  }
  const stamp = `${embedder.model}@${INDEXER_VERSION}`;
  const previous = stampOf(db, "embeddings");
  if (previous !== stamp) db.exec("DELETE FROM embeddings");
  const todo = db
    .prepare("SELECT s.id, s.name, s.signature, s.body FROM symbols s LEFT JOIN embeddings e ON e.symbol_id = s.id WHERE e.symbol_id IS NULL AND s.kind != 'class' ORDER BY s.id")
    .all() as { id: number; name: string; signature: string; body: string }[];
  try {
    const vectors = await embedder.embed(todo.map(embeddingText));
    db.transaction(() => {
      todo.forEach((s, i) => db.prepare("INSERT OR REPLACE INTO embeddings (symbol_id, model, vector) VALUES (?, ?, ?)").run(s.id, embedder.model, encodeVec(vectors[i])));
    })();
    setLayer(db, "embeddings", stamp, "ok", `${embedder.model} on loopback`, now);
  } catch (e) {
    // The other layers stay usable; the next build retries (Review Focus 3).
    setLayer(db, "embeddings", previous ?? "none", "unavailable", (e as Error).message, now);
  }
}

export async function buildIndex(deps: Deps, loaded: LoadedProfile, repo: string, o: { full: boolean; quick?: boolean; mirror?: boolean }, providers: Providers): Promise<BuildReport> {
  const cfg = loaded.repos[repo];
  if (cfg === undefined) throw new SindriError("SND-PROFILE-004", `no repo named ${repo}`);
  const ix = loaded.profile.index;
  const quick = o.quick === true;
  return withHeavyLock(deps, `index-build:${repo}`, 600_000, async () => {
    const started = deps.now().getTime();
    // Inside the heavy-job lock: a clone of a big repo is heavy too.
    if (o.mirror === true) await refreshMirror(deps, repo, cfg.path);
    const deny = [...ix.denyPaths, ...cfg.index.denyPaths];
    const inv = await inventory(deps.git, cfg.path, {
      denyPaths: deny,
      maxFileKB: ix.maxFileKB,
      maxTotalMB: ix.maxTotalMB,
      select: (p) => isSourcePath(p) || p === "package.json" || p.endsWith("/package.json"),
    });
    const live = indexPath(deps, repo);
    fs.mkdirSync(path.dirname(live), { recursive: true, mode: 0o700 });
    sweepTmp(deps, live);
    // Build into a temp copy and rename it over the live file: a crash or a full
    // disk leaves the previous complete index in place (Review Focus 2).
    const tmp = `${live}.tmp-${ulid(deps.now())}`;
    if (!o.full && fs.existsSync(live)) fs.copyFileSync(live, tmp);
    const db = openIndex(tmp);
    try {
      const now = deps.now();
      const stamp = structureStamp(ix.utilityGlobs, loaded.profile.scrub.extraPatterns);
      const scrubber = makeScrubber(compileExtraPatterns(loaded.profile.scrub.extraPatterns));
      const { changed, removed } = writeStructure(db, inv.files, ix.utilityGlobs, stamp, scrubber);
      setLayer(db, "structure", stamp, "ok", "TypeScript compiler API", now);
      setLayer(db, "clones", stamp, "ok", "AST hash + MinHash/LSH", now);
      writeDeps(db, inv.files);
      setLayer(db, "deps", `deps@${INDEXER_VERSION}`, "ok", "package.json manifests", now);
      if (quick) {
        // Quick builds never touch the network layers; a fresh index marks them pending.
        for (const layer of ["embeddings", "graph"] as const) {
          if (stampOf(db, layer) === null) setLayer(db, layer, "none", "pending", "not built yet (sindri index build)", now);
        }
      } else {
        // Task 7 replaces the graph line with the provider-backed layer.
        await embedLayer(db, providers.embedder, now);
        setLayer(db, "graph", "none", "disabled", "no graph provider configured", now);
      }
      const head = await deps.git.run(["rev-parse", "HEAD"], cfg.path);
      const commit = head.ok ? head.stdout.trim() : null;
      db.prepare("INSERT OR REPLACE INTO meta (key, value) VALUES ('commit', ?), ('built_at', ?)").run(commit ?? "", now.toISOString());
      const symbols = (db.prepare("SELECT COUNT(*) AS n FROM symbols").get() as { n: number }).n;
      const report: BuildReport = {
        repo,
        commit,
        quick,
        files: { indexed: inv.files.length, changed, removed, skipped: inv.skipped.length },
        symbols,
        layers: Object.fromEntries((db.prepare("SELECT layer, status, detail FROM layers").all() as { layer: Layer; status: LayerStatus; detail: string }[]).map((l) => [l.layer, { status: l.status, detail: l.detail }])) as BuildReport["layers"],
        ms: deps.now().getTime() - started,
      };
      db.close();
      fs.renameSync(tmp, live);
      return report;
    } catch (e) {
      if (db.open) db.close();
      fs.rmSync(tmp, { force: true });
      throw e;
    }
  });
}
