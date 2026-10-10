import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { indexPath, type IndexDb } from "../index/db.js";
import { readBuiltIndex, readIndex } from "../index/health.js";
import type { LoadedProfile } from "../profile/load.js";
import { stripInvisible, WHITESPACE } from "./invisible.js";

// Layer 3 of the publish gate (spec §5.4): code identifiers from private repos' indexes, minus English
// words and the toolkit's own vocabulary. Built in memory at publish; never written, logged or printed.
const MIN_IDENTIFIER = 5;
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

export function buildDerivedSet(o: { privateVocab: readonly ReadonlySet<string>[]; toolkit: ReadonlySet<string>; words: ReadonlySet<string> }): Set<string> {
  const out = new Set<string>();
  for (const vocab of o.privateVocab) for (const t of vocab) if (!o.words.has(t) && !o.toolkit.has(t)) out.add(t);
  return out;
}

// The repos publish treats as private: `private: true` in their repo file. The tracker repo is the public publish target
// by definition, so it is never one, even if it sets the flag. Doctor and publish both decide it here.
export function privateRepos(loaded: LoadedProfile): string[] {
  return Object.keys(loaded.repos).sort().filter((name) => name !== loaded.profile.tracker.repo && loaded.repos[name].private === true);
}

// Repos other than the tracker repo that don't say `private:` either way; publish treats them as public.
export function unsetPrivateRepos(loaded: LoadedProfile): string[] {
  return Object.keys(loaded.repos).sort().filter((name) => name !== loaded.profile.tracker.repo && loaded.repos[name].private === undefined);
}

export function loadDerivedSet(deps: Deps, loaded: LoadedProfile): Set<string> {
  const priv = privateRepos(loaded);
  if (priv.length === 0) return new Set();
  const wf = wordsFile(deps);
  if (!fs.existsSync(wf)) throw new SindriError("SND-EVOLVE-018", `the word list ${wf} is missing, so publish can't tell private identifiers from English words`);
  const words = new Set(fs.readFileSync(wf, "utf8").split("\n").map((w) => w.trim().toLowerCase()).filter((w) => w !== ""));
  const privateVocab = priv.map((name) => {
    // Fail closed: no index, an unbuilt one or an unusable one (a missing table, not a database) refuses.
    const file = indexPath(deps, name);
    const r = readBuiltIndex(file, vocabularyOf);
    if (r.state === "damaged") {
      throw new SindriError("SND-EVOLVE-017", `repo ${name} is private but its code index is unusable (${r.detail})`, { fix: `rm ${file}, then sindri index build --repo ${name}, then rerun sindri evolve publish` });
    }
    if (r.state !== "ok") throw new SindriError("SND-EVOLVE-017", `repo ${name} is private but has no built code index`, { fix: `sindri index build --repo ${name}, then rerun sindri evolve publish` });
    return r.value;
  });
  // No usable toolkit index subtracts nothing: more identifiers are held, never fewer.
  const tk = readIndex(indexPath(deps, loaded.profile.tracker.repo), vocabularyOf);
  return buildDerivedSet({ privateVocab, toolkit: tk.state === "ok" ? tk.value : new Set(), words });
}

export function identifierProblem(text: string, derived: ReadonlySet<string>): string | null {
  if (derived.size === 0) return null;
  return identifierTokens(text).some((t) => derived.has(t)) ? "contains a private-repo identifier" : null;
}
