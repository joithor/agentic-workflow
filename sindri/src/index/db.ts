import fs from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";

import { stateDir, type Deps } from "../deps.js";
import type { DepRow } from "./deps-layer.js";
import { decodeSig } from "./minhash.js";

export type IndexDb = Database.Database;
export const LAYERS = ["structure", "clones", "deps", "embeddings", "graph"] as const;
export type Layer = (typeof LAYERS)[number];
// "pending": a quick build ran before the first full one, so the layer was never built.
export type LayerStatus = "ok" | "unavailable" | "disabled" | "pending";

// The index is derived data: a different schema version is rebuilt, not migrated.
export const INDEX_SCHEMA_VERSION = 1;
const SCHEMA = `
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE layers (layer TEXT PRIMARY KEY, stamp TEXT NOT NULL, status TEXT NOT NULL, detail TEXT NOT NULL, built_at TEXT NOT NULL);
CREATE TABLE files (path TEXT PRIMARY KEY, hash TEXT NOT NULL, size INTEGER NOT NULL);
CREATE TABLE symbols (
  id INTEGER PRIMARY KEY,
  file TEXT NOT NULL REFERENCES files(path) ON DELETE CASCADE,
  name TEXT NOT NULL, kind TEXT NOT NULL, start_line INTEGER NOT NULL, end_line INTEGER NOT NULL,
  exported INTEGER NOT NULL, utility INTEGER NOT NULL, signature TEXT NOT NULL, ast_hash TEXT NOT NULL,
  token_count INTEGER NOT NULL, complexity INTEGER NOT NULL, callees TEXT NOT NULL, minhash BLOB NOT NULL, body TEXT NOT NULL
);
CREATE INDEX symbols_ast ON symbols(ast_hash);
CREATE INDEX symbols_name ON symbols(name);
CREATE INDEX symbols_file ON symbols(file);
CREATE TABLE bands (key TEXT NOT NULL, symbol_id INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE);
CREATE INDEX bands_key ON bands(key);
CREATE INDEX bands_symbol ON bands(symbol_id);
CREATE TABLE deps (manifest TEXT NOT NULL, name TEXT NOT NULL, version TEXT NOT NULL, kind TEXT NOT NULL, tags TEXT NOT NULL, PRIMARY KEY (manifest, name));
CREATE TABLE embeddings (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, model TEXT NOT NULL, vector BLOB NOT NULL);
CREATE TABLE graph_nodes (id TEXT PRIMARY KEY, file TEXT, name TEXT, line INTEGER);
CREATE TABLE graph_edges (src TEXT NOT NULL, dst TEXT NOT NULL, relation TEXT NOT NULL, confidence TEXT NOT NULL);
`;

export function indexPath(deps: Deps, repo: string): string {
  return path.join(stateDir(deps), "index", `${repo}.db`);
}

export function openIndex(file: string): IndexDb {
  fs.mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  let db = new Database(file);
  if (db.pragma("user_version", { simple: true }) !== INDEX_SCHEMA_VERSION) {
    db.close();
    fs.rmSync(file, { force: true });
    db = new Database(file);
    db.exec(SCHEMA);
    db.pragma(`user_version = ${INDEX_SCHEMA_VERSION}`);
  }
  db.pragma("foreign_keys = ON");
  fs.chmodSync(file, 0o600);
  return db;
}

// Readers (shape --record, query, status, doctor) never see an index of another schema version.
export function openIndexReadOnly(file: string): IndexDb | null {
  if (!fs.existsSync(file)) return null;
  const db = new Database(file, { readonly: true });
  if (db.pragma("user_version", { simple: true }) !== INDEX_SCHEMA_VERSION) {
    db.close();
    return null;
  }
  return db;
}

export interface SymbolRow {
  id: number;
  file: string;
  name: string;
  kind: string;
  startLine: number;
  endLine: number;
  exported: boolean;
  utility: boolean;
  signature: string;
  astHash: string;
  tokenCount: number;
  complexity: number;
  callees: string[];
  minhash: Uint32Array;
}

interface RawSymbol {
  id: number; file: string; name: string; kind: string; start_line: number; end_line: number; exported: number; utility: number;
  signature: string; ast_hash: string; token_count: number; complexity: number; callees: string; minhash: Buffer;
}

// Everything but `body`: bodies are only read back for embeddings.
const COLUMNS = "id, file, name, kind, start_line, end_line, exported, utility, signature, ast_hash, token_count, complexity, callees, minhash";

const toRow = (r: RawSymbol): SymbolRow => ({
  id: r.id, file: r.file, name: r.name, kind: r.kind, startLine: r.start_line, endLine: r.end_line, exported: r.exported === 1,
  utility: r.utility === 1, signature: r.signature, astHash: r.ast_hash, tokenCount: r.token_count, complexity: r.complexity,
  callees: JSON.parse(r.callees) as string[], minhash: decodeSig(r.minhash),
});

export function symbolsByAstHash(db: IndexDb, hash: string): SymbolRow[] {
  return (db.prepare(`SELECT ${COLUMNS} FROM symbols WHERE ast_hash = ? ORDER BY file, start_line`).all(hash) as RawSymbol[]).map(toRow);
}

export function allSymbols(db: IndexDb): SymbolRow[] {
  return (db.prepare(`SELECT ${COLUMNS} FROM symbols ORDER BY file, start_line`).all() as RawSymbol[]).map(toRow);
}

export function bandCandidates(db: IndexDb, keys: string[]): number[] {
  if (keys.length === 0) return [];
  const rows = db.prepare(`SELECT DISTINCT symbol_id FROM bands WHERE key IN (${keys.map(() => "?").join(",")})`).all(...keys) as { symbol_id: number }[];
  return rows.map((r) => r.symbol_id);
}

export function depRows(db: IndexDb): DepRow[] {
  return (db.prepare("SELECT * FROM deps ORDER BY manifest, name").all() as (Omit<DepRow, "tags"> & { tags: string })[]).map((r) => ({
    ...r,
    tags: JSON.parse(r.tags) as string[],
  }));
}

export function embeddingRows(db: IndexDb, model: string): { symbolId: number; vector: Buffer }[] {
  return (db.prepare("SELECT symbol_id, vector FROM embeddings WHERE model = ?").all(model) as { symbol_id: number; vector: Buffer }[]).map((r) => ({
    symbolId: r.symbol_id,
    vector: r.vector,
  }));
}

export function graphEdges(db: IndexDb): { src: string; dst: string; relation: string; confidence: string }[] {
  return db.prepare("SELECT * FROM graph_edges").all() as { src: string; dst: string; relation: string; confidence: string }[];
}

export function layers(db: IndexDb): { layer: Layer; stamp: string; status: LayerStatus; detail: string; builtAt: string }[] {
  return (db.prepare("SELECT * FROM layers ORDER BY layer").all() as { layer: Layer; stamp: string; status: LayerStatus; detail: string; built_at: string }[]).map(
    (r) => ({ layer: r.layer, stamp: r.stamp, status: r.status, detail: r.detail, builtAt: r.built_at }),
  );
}

export function meta(db: IndexDb): { commit: string; builtAt: string | null } {
  const get = (k: string) => (db.prepare("SELECT value FROM meta WHERE key = ?").get(k) as { value: string } | undefined)?.value;
  return { commit: get("commit") ?? "", builtAt: get("built_at") ?? null };
}
