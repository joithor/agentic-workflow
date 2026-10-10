import fs from "node:fs";
import path from "node:path";

import type { GitRunner } from "../git.js";
import { stripInvisible, WHITESPACE } from "./invisible.js";

// The publish gate's layers, in the order they run (spec §5.4); `generalize` holds a proposal with no public version.
export const HOLD_LAYERS = ["generalize", "terms", "patterns", "identifiers", "verbatim", "personal", "scrubber"] as const;
export type HoldLayer = (typeof HOLD_LAYERS)[number];

// The publication gate (spec amendment 2): nothing workplace-specific reaches the public repo.
// Matching runs on a normalized copy, so a term can't be hidden behind a line break, a double space,
// an NBSP, a zero-width or soft-hyphen character, a quote prefix, fullwidth letters or a decomposed accent.
const EMAIL = /[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}/;
// An absolute home directory with a user segment, also behind a file:// prefix (the third slash is the path's own).
// Case-sensitive on purpose: "/users/me" and "github.com/users/foo" are routes, not homes; the current OS user's own
// name is held in any case (macOS paths are case-insensitive), below.
const HOME_UNIX = /(?<![A-Za-z0-9._-])\/(?:Users|home)\/[A-Za-z0-9._-]+/;
// Claude Code's project directory encoding of /Users/<name>/...: "-Users-<name>-...".
const CLAUDE_DIR = /(?<![A-Za-z0-9])-Users-[A-Za-z0-9_.]+/;
const HOME_WINDOWS = /[a-z]:\\+users\\+[a-z0-9._-]+/i;
const escapeRe = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

// Case and quote prefixes stay; invisibles go. Used where case matters (the home-path check).
const visibleForm = (text: string): string => stripInvisible(text.normalize("NFKC"), WHITESPACE);

export function normalizeForPrivacy(text: string): string {
  return stripInvisible(text.replace(/[\u0131\u0130]/g, "i").normalize("NFKD"), WHITESPACE).replace(/\p{M}/gu, "").normalize("NFKC")
    .replace(/^[ \t]*>+[ \t]?/gm, "")
    .toLowerCase()
    .replace(/[\s\u0085]+/g, " ")
    .trim();
}

export function termProblem(text: string, denyTerms: readonly string[]): string | null {
  const t = normalizeForPrivacy(text);
  for (const term of denyTerms.map(normalizeForPrivacy).filter((x) => x !== "")) {
    if (new RegExp(`(?<![a-z0-9])${escapeRe(term)}(?![a-z0-9])`).test(t)) return "contains a private term";
  }
  return null;
}

// `osUser` is the current OS user (from the system seam): their home path is held in any case and in Claude's encoding.
export function personalProblem(text: string, osUser = ""): string | null {
  const t = normalizeForPrivacy(text);
  const visible = visibleForm(text);
  const me = osUser === "" ? null : escapeRe(osUser.normalize("NFKC"));
  const mine = me === null ? false : new RegExp(`(?<![A-Za-z0-9._-])\\/(?:users|home)\\/${me}(?![A-Za-z0-9._-])|(?<![A-Za-z0-9])-(?:users|home)-${me}(?![A-Za-z0-9])`, "i").test(visible);
  return EMAIL.test(t) || HOME_UNIX.test(visible) || CLAUDE_DIR.test(visible) || mine || HOME_WINDOWS.test(t) ? "contains an email address or a home directory path" : null;
}

export function privacyProblem(text: string, denyTerms: readonly string[], osUser = ""): string | null {
  return termProblem(text, denyTerms) ?? personalProblem(text, osUser);
}

// Publish-only regexes (privacy.denyPatterns), tried on the visible text and on the fully normalized copy, so
// fullwidth letters, invisibles, accents and line breaks don't hide a match.
export function patternProblem(text: string, patterns: readonly string[]): string | null {
  if (patterns.length === 0) return null;
  const forms = [visibleForm(text), normalizeForPrivacy(text)];
  return patterns.some((p) => forms.some((f) => new RegExp(p, "iu").test(f))) ? "matches a privacy.denyPatterns entry" : null;
}

// Verbatim overlap (spec 5.5): a run of RUN_WORDS normalized words shared with a transcript line is a quote.
export const RUN_WORDS = 8;
const wordsOf = (text: string): string[] => normalizeForPrivacy(text).split(/[^\p{L}\p{N}]+/u).filter((w) => w !== "");
export const runWords = (text: string): string => wordsOf(text).join(" ");

function runsOf(text: string): Set<string> {
  const w = wordsOf(text);
  const out = new Set<string>();
  for (let i = 0; i + RUN_WORDS <= w.length; i++) out.add(w.slice(i, i + RUN_WORDS).join(" "));
  return out;
}

// `isPublic(run)` says whether the run already appears in the toolkit's own tracked text; those are not leaks.
export function copiedRun(text: string, sources: readonly string[], isPublic: (run: string) => boolean): boolean {
  const mine = runsOf(text);
  if (mine.size === 0) return false;
  return sources.some((s) => [...runsOf(s)].some((run) => mine.has(run) && !isPublic(run)));
}

const CORPUS_FILE_MAX = 1_000_000;

// The toolkit's own tracked text, as normalized words: a run found here is public already (spec §5.5). Symlinks,
// binaries, files over 1 MB and tracked files missing from the work tree are skipped.
export async function toolkitCorpus(git: GitRunner, repo: string): Promise<string> {
  const r = await git.run(["ls-files", "-z"], repo);
  if (!r.ok) return "";
  const parts: string[] = [];
  for (const rel of r.stdout.split("\0").filter((f) => f !== "")) {
    const st = fs.lstatSync(path.join(repo, rel), { throwIfNoEntry: false });
    if (st === undefined || !st.isFile() || st.size > CORPUS_FILE_MAX) continue;
    const buf = fs.readFileSync(path.join(repo, rel));
    if (buf.includes(0)) continue; // binary
    parts.push(runWords(buf.toString("utf8")));
  }
  // "|" is never a word, so a run can't span the end of one file and the start of the next.
  return ` ${parts.join(" | ")} `;
}
