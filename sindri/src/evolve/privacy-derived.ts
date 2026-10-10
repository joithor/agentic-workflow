import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { indexPath, layers, openIndexReadOnly, type IndexDb } from "../index/db.js";
import type { LoadedProfile } from "../profile/load.js";
import { stripInvisible, WHITESPACE } from "./invisible.js";

// Layer 3 of the publish gate (spec §5.4): code identifiers from private repos' indexes, minus English
// words and the toolkit's own vocabulary. Built in memory at publish; never written, logged or printed.
export const MIN_IDENTIFIER = 5;
const IDENT = /[A-Za-z_$][A-Za-z0-9_$]*/g;

export const wordsFile = (deps: Deps): string => deps.env.SINDRI_WORDS ?? "/usr/share/dict/words";

export function identifierTokens(text: string): string[] {
  const visible = stripInvisible(text.normalize("NFKC"), WHITESPACE);
  return (visible.match(IDENT) ?? []).map((t) => t.toLowerCase()).filter((t) => t.length >= MIN_IDENTIFIER);
}

export function vocabularyOf(db: IndexDb): Set<string> {
  const out = new Set<string>();
  for (const r of db.prepare("SELECT name FROM symbols").all() as { name: string }[]) identifierTokens(r.name).forEach((t) => out.add(t));
  for (const r of db.prepare("SELECT path FROM files").all() as { path: string }[]) {
    for (const seg of r.path.split("/")) identifierTokens(seg.slice(0, seg.length - path.posix.extname(seg).length)).forEach((t) => out.add(t));
  }
  return out;
}

export function buildDerivedSet(o: { privateDbs: readonly IndexDb[]; toolkit: IndexDb | null; words: ReadonlySet<string> }): Set<string> {
  const toolkit = o.toolkit === null ? new Set<string>() : vocabularyOf(o.toolkit);
  const out = new Set<string>();
  for (const db of o.privateDbs) for (const t of vocabularyOf(db)) if (!o.words.has(t) && !toolkit.has(t)) out.add(t);
  return out;
}

const structureOk = (db: IndexDb): boolean => layers(db).some((l) => l.layer === "structure" && l.status === "ok");

export function loadDerivedSet(deps: Deps, loaded: LoadedProfile): Set<string> {
  const priv = Object.keys(loaded.repos).sort().filter((name) => loaded.repos[name].private === true);
  if (priv.length === 0) return new Set();
  const wf = wordsFile(deps);
  if (!fs.existsSync(wf)) throw new SindriError("SND-EVOLVE-018", `the word list ${wf} is missing, so publish can't tell private identifiers from English words`);
  const words = new Set(fs.readFileSync(wf, "utf8").split("\n").map((w) => w.trim().toLowerCase()).filter((w) => w !== ""));
  const opened: IndexDb[] = [];
  try {
    for (const name of priv) {
      const db = openIndexReadOnly(indexPath(deps, name));
      if (db !== null) opened.push(db);
      if (db === null || !structureOk(db)) {
        throw new SindriError("SND-EVOLVE-017", `repo ${name} is private but has no built code index`, { fix: `sindri index build --repo ${name}, then rerun sindri evolve publish` });
      }
    }
    const toolkit = openIndexReadOnly(indexPath(deps, loaded.profile.tracker.repo));
    if (toolkit !== null) opened.push(toolkit);
    return buildDerivedSet({ privateDbs: opened.filter((d) => d !== toolkit), toolkit, words });
  } finally {
    opened.forEach((d) => d.close());
  }
}

export function identifierProblem(text: string, derived: ReadonlySet<string>): string | null {
  if (derived.size === 0) return null;
  return identifierTokens(text).some((t) => derived.has(t)) ? "contains a private-repo identifier" : null;
}
