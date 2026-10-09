import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";

import { stateDir, type Deps } from "../deps.js";
import { ledgerPath, readLedger } from "../ledger/db.js";
import { makeScrubber } from "../scrub/scrub.js";
import type { SourceRecord } from "../scope/source.js";

export interface ReplayItem {
  id: string;
  artifact: "scope.draft";
  createdAt: string;
  brief: SourceRecord;
  records: SourceRecord[];
  outcome: { status: string; surfaces: number; recall: number | null };
}

const Rec = z.object({
  ref: z.string(), kind: z.enum(["brief", "doc", "note", "transcript", "issue", "comment", "code"]), title: z.string(), text: z.string(),
  author: z.string().nullable(), createdAt: z.string().nullable(), trust: z.enum(["trusted", "untrusted"]),
});
const Item = z.object({
  id: z.string().regex(/^[0-9a-z-]+$/), artifact: z.literal("scope.draft"), createdAt: z.string(), brief: Rec, records: z.array(Rec),
  outcome: z.object({ status: z.string(), surfaces: z.number(), recall: z.number().nullable() }),
});
const ManifestLine = z.object({ id: z.string(), sha256: z.string().regex(/^[0-9a-f]{64}$/), added_at: z.string() });

const scrubber = makeScrubber();
const sha = (b: string | Buffer): string => createHash("sha256").update(b).digest("hex");

export const corpusDir = (deps: Deps): string => path.join(stateDir(deps), "corpus");
const artifactDir = (deps: Deps, artifact: ReplayItem["artifact"]): string => path.join(corpusDir(deps), artifact);

function endsMidLine(file: string): boolean {
  if (!fs.existsSync(file)) return false;
  const b = fs.readFileSync(file);
  return b.length > 0 && b[b.length - 1] !== 0x0a;
}

// Scrubbed on write, created exclusively (an id is never overwritten), and recorded in an
// append-only manifest. loadCorpus ignores anything the manifest doesn't vouch for.
export function saveReplay(deps: Deps, item: ReplayItem): boolean {
  const parsed = Item.parse(scrubber.scrubDeep(item));
  const dir = artifactDir(deps, parsed.artifact);
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  const bytes = JSON.stringify(parsed);
  try {
    fs.writeFileSync(path.join(dir, `${parsed.id}.json`), bytes, { flag: "wx", mode: 0o600 });
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code === "EEXIST") return false;
    throw e;
  }
  const manifest = path.join(dir, "manifest.jsonl");
  const line = JSON.stringify({ id: parsed.id, sha256: sha(bytes), added_at: deps.now().toISOString() });
  try {
    // A torn last line (no trailing newline) must not swallow this one.
    fs.appendFileSync(manifest, `${endsMidLine(manifest) ? "\n" : ""}${line}\n`, { mode: 0o600 });
  } catch (e) {
    fs.rmSync(path.join(dir, `${parsed.id}.json`), { force: true }); // no orphan an unlisted file would be flagged for ever
    throw e;
  }
  return true;
}

// A scope run must never fail because its replay couldn't be saved.
export function trySaveReplay(deps: Deps, item: ReplayItem): boolean {
  try {
    return saveReplay(deps, item);
  } catch {
    return false;
  }
}

function readManifest(file: string): Map<string, string> {
  const out = new Map<string, string>();
  if (!fs.existsSync(file)) return out;
  for (const raw of fs.readFileSync(file, "utf8").split("\n")) {
    let parsed: z.SafeParseReturnType<unknown, z.infer<typeof ManifestLine>>;
    try {
      parsed = ManifestLine.safeParse(JSON.parse(raw));
    } catch {
      continue;
    }
    if (parsed.success && !out.has(parsed.data.id)) out.set(parsed.data.id, parsed.data.sha256); // append-only: the first line for an id wins
  }
  return out;
}

// The run ids the ledger recorded, read-only; null when the ledger can't be read.
function recordedRuns(deps: Deps): Set<string> | null {
  const file = ledgerPath(stateDir(deps));
  if (!fs.existsSync(file)) return null;
  try {
    return readLedger(file, (db) => new Set((db.prepare("SELECT run_id FROM scope_runs").all() as { run_id: string }[]).map((r) => r.run_id)));
  } catch {
    return null;
  }
}

// An item counts only when the manifest vouches for its bytes, its content id is its filename, and the
// ledger has a scope_runs row for that run (a manifest alone is no seal against a state-dir writer).
// With no readable ledger nothing can be verified, so every item is dropped and `unverified` says why.
export function readCorpus(deps: Deps, artifact: ReplayItem["artifact"]): { items: ReplayItem[]; dropped: string[]; unverified: string | null } {
  const dir = artifactDir(deps, artifact);
  if (!fs.existsSync(dir)) return { items: [], dropped: [], unverified: null };
  const runs = recordedRuns(deps);
  const manifest = readManifest(path.join(dir, "manifest.jsonl"));
  const items: ReplayItem[] = [];
  const dropped: string[] = [];
  for (const n of fs.readdirSync(dir).filter((x) => x.endsWith(".json")).sort()) {
    const id = n.slice(0, -".json".length);
    const bytes = fs.readFileSync(path.join(dir, n));
    if (runs === null || !runs.has(id) || manifest.get(id) !== sha(bytes)) {
      dropped.push(id);
      continue;
    }
    try {
      const parsed = Item.parse(JSON.parse(bytes.toString("utf8")));
      if (parsed.id === id) items.push(parsed);
      else dropped.push(id);
    } catch {
      dropped.push(id);
    }
  }
  return { items, dropped, unverified: runs === null && dropped.length > 0 ? "the ledger couldn't be read, so no corpus item can be verified against a recorded run" : null };
}

export const loadCorpus = (deps: Deps, artifact: ReplayItem["artifact"]): ReplayItem[] => readCorpus(deps, artifact).items;

// ≈30% sealed holdout (spec §7.4): a pure function of the id, never of the content, so no
// proposal generator can steer which items end up in it.
export function isHoldout(id: string): boolean {
  return createHash("sha256").update(id).digest()[0] < 0x4d;
}

export function split(items: ReplayItem[]): { train: ReplayItem[]; holdout: ReplayItem[] } {
  return { train: items.filter((i) => !isHoldout(i.id)), holdout: items.filter((i) => isHoldout(i.id)) };
}

// reflect and correct drop any turn that quotes a holdout brief's title (12+ characters), so the
// proposals they generate can't have seen the sealed items.
export function holdoutTitles(deps: Deps): string[] {
  return split(loadCorpus(deps, "scope.draft")).holdout.map((i) => i.brief.title).filter((t) => t.length >= 12);
}

export const mentionsHoldout = (text: string, titles: readonly string[]): boolean => titles.some((t) => text.toLowerCase().includes(t.toLowerCase()));
