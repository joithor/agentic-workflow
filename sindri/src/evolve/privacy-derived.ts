import fs from "node:fs";
import path from "node:path";

import type { Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { indexPath, type IndexDb } from "../index/db.js";
import { readBuiltIndex, readIndex } from "../index/health.js";
import type { LoadedProfile } from "../profile/load.js";
import { stripInvisible, WHITESPACE } from "./invisible.js";
import { NAME_JOINER, splitCase } from "./privacy.js";

// Layer 3 of the publish gate (spec §5.4): code identifiers from private repos' indexes, minus English
// words and the toolkit's own vocabulary. Built in memory at publish; never written, logged or printed.
const MIN_IDENTIFIER = 5;
// Unicode-aware on both sides (the derived set and the candidate): a non-ASCII letter is an identifier character, so it
// can't split a token.
const IDENT = /[\p{L}_$][\p{L}\p{N}_$]*/gu;

export const wordsFile = (deps: Deps): string => deps.env.SINDRI_WORDS ?? "/usr/share/dict/words";

// A name spelled with separators: parts joined by any NAME_JOINER (`nova-ledger`, `nova\u2011ledger`,
// `order_sync_worker`, `pkg.name`), the same class on the derived set's side and the candidate's. Its joined form drops
// every character that isn't a letter or a digit.
const PART = "[\\p{L}\\p{N}$]+";
const SPELLED = new RegExp(`${PART}(?:${NAME_JOINER.source}+${PART})*`, "gu");
const joinedForm = (run: string): string => run.replace(/[^\p{L}\p{N}]/gu, "");

// NFKD, invisibles and combining marks (\p{M}) removed, lowercased: a decomposed or precomposed accent, fullwidth
// letters and zero-width characters neither split an identifier nor change it. Besides each identifier, the joined
// form of every name spelled with separators (security-sentinel-1): `nova-ledger`, `nova_ledger`, `novaLedger` and
// `NovaLedger` all give `novaledger`, on the derived set's side and the candidate's alike, so no spelling of a private
// name slips past another. Both kinds need at least 5 characters, and a joined form needs a letter.
const letters = (text: string): string => stripInvisible(text.normalize("NFKD"), WHITESPACE).replace(/\p{M}/gu, "");
const fold = (text: string): string => letters(text).toLowerCase();
const plainTokens = (folded: string): string[] => (folded.match(IDENT) ?? []).filter((t) => t.length >= MIN_IDENTIFIER);
const keepJoined = (t: string): boolean => t.length >= MIN_IDENTIFIER && /\p{L}/u.test(t);

export function identifierTokens(text: string): string[] {
  const visible = fold(text);
  const joined = (visible.match(SPELLED) ?? []).map(joinedForm).filter(keepJoined);
  return [...new Set([...plainTokens(visible), ...joined])];
}

// The candidate side (run 4 kieran-typescript-reviewer-0): a private name is often written with more parts around it
// (`nova-ledger.ts`, `@acme/nova-ledger-client`, `nova-ledger-v2`), so besides the whole run, the joined form of each
// contiguous sub-run of 2 to MAX_SUB_PARTS parts counts too. The derived set keeps whole runs only, so a private
// `glx-rota-utils` never puts `glxrota` in the set. Bounded: a run of n parts gives under n * MAX_SUB_PARTS forms.
// Parts also split at case and letter-digit boundaries, by the terms layer's rules, on the text before it is lowercased
// (run 5 security-sentinel-2, kieran-typescript-reviewer-1): `NovaLedgerClient`, `useNovaLedger`, `createNovaLedgerV2`
// and `nova_ledgerClient` hold for a private `novaledger`, `SupernovaLedger` doesn't (its part is `supernova`). Sub-runs
// cross both kinds of boundary; the whole token still counts as written. The set side stays whole-token.
const MAX_SUB_PARTS = 8;
const JOINERS = new RegExp(`${NAME_JOINER.source}+`, "u");
function candidateTokens(text: string): string[] {
  const visible = fold(text);
  const joined: string[] = [];
  for (const run of splitCase(letters(text), "_").toLowerCase().match(SPELLED) ?? []) {
    joined.push(joinedForm(run));
    const parts = run.split(JOINERS);
    for (let i = 0; i < parts.length; i++) {
      for (let j = i + 2; j <= Math.min(parts.length, i + MAX_SUB_PARTS); j++) joined.push(joinedForm(parts.slice(i, j).join("")));
    }
  }
  return [...new Set([...plainTokens(visible), ...joined.filter(keepJoined)])];
}

export function vocabularyOf(db: IndexDb): Set<string> {
  const out = new Set<string>();
  for (const r of db.prepare("SELECT name FROM symbols").all() as { name: string }[]) identifierTokens(r.name).forEach((t) => out.add(t));
  pathVocabulary((db.prepare("SELECT path FROM files").all() as { path: string }[]).map((r) => r.path)).forEach((t) => out.add(t));
  return out;
}

// Each directory (whole: a dot in `nova.ledger/` joins a name, it isn't an extension) and the basename without its
// extension of each path, as identifier tokens.
export function pathVocabulary(paths: readonly string[]): Set<string> {
  const out = new Set<string>();
  for (const p of paths) {
    const segs = p.split("/");
    const base = segs.pop() as string;
    for (const seg of [...segs, base.slice(0, base.length - path.posix.extname(base).length)]) identifierTokens(seg).forEach((t) => out.add(t));
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

export async function loadDerivedSet(deps: Deps, loaded: LoadedProfile): Promise<Set<string>> {
  const priv = privateRepos(loaded);
  if (priv.length === 0) return new Set();
  const wf = wordsFile(deps);
  if (!fs.existsSync(wf)) throw new SindriError("SND-EVOLVE-018", `the word list ${wf} is missing, so publish can't tell private identifiers from English words`);
  const words = new Set(fs.readFileSync(wf, "utf8").split("\n").map((w) => w.trim().toLowerCase()).filter((w) => w !== ""));
  const privateVocab: Set<string>[] = [];
  for (const name of priv) {
    // Fail closed: no index, an unbuilt one or an unusable one (a missing table, not a database) refuses.
    const file = indexPath(deps, name);
    const r = readBuiltIndex(file, vocabularyOf);
    if (r.state === "damaged") {
      throw new SindriError("SND-EVOLVE-017", `repo ${name} is private but its code index is unusable (${r.detail})`, { fix: `rm ${file}, then sindri index build --repo ${name}, then rerun sindri evolve publish` });
    }
    if (r.state !== "ok") throw new SindriError("SND-EVOLVE-017", `repo ${name} is private but has no built code index`, { fix: `sindri index build --repo ${name}, then rerun sindri evolve publish` });
    // The index skips denyPaths, secret globs, oversized and non-source files; their names still count, so every
    // tracked path is read from git too, submodules included (names only, never contents). Fail closed when git can't list them.
    const ls = await deps.git.run(["ls-files", "-z", "--recurse-submodules"], loaded.repos[name].path, { foreign: true });
    if (!ls.ok) {
      throw new SindriError("SND-EVOLVE-019", `couldn't list the tracked files of private repo ${name}, so publish can't check proposals for their names`, {
        fix: `check that repos/${name}.yaml's path is that repo's git checkout on this machine, then rerun sindri evolve publish`,
      });
    }
    privateVocab.push(new Set([...r.value, ...pathVocabulary(ls.stdout.split("\0").filter((f) => f !== ""))]));
  }
  // No usable toolkit index subtracts nothing: more identifiers are held, never fewer.
  const tk = readIndex(indexPath(deps, loaded.profile.tracker.repo), vocabularyOf);
  return buildDerivedSet({ privateVocab, toolkit: tk.state === "ok" ? tk.value : new Set(), words });
}

export function identifierProblem(text: string, derived: ReadonlySet<string>): string | null {
  if (derived.size === 0) return null;
  return candidateTokens(text).some((t) => derived.has(t)) ? "contains a private-repo identifier" : null;
}
