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

// Separators a multi-part name is written with: every character that is not a letter or a digit (spaces, ASCII and
// Unicode dashes, slashes, dots, underscores, middle dots, apostrophes and other punctuation, symbols and spaces), so
// "acme care", "acme-care", "acme\u2011care", "acme/care", "AcmeCare" all join to "acmecare".
const SEPARATORS = /[^\p{L}\p{N}]+/gu;

// Whole words; and a term with separators also matches with them removed on both sides, so every joined or
// re-separated spelling holds without the owner listing it (holds only: a false positive such as "acme carefully"
// is accepted). A term without separators stays whole-word: "acme" doesn't hold "acmeville".
export function termProblem(text: string, denyTerms: readonly string[]): string | null {
  const t = normalizeForPrivacy(text);
  const joined = t.replace(SEPARATORS, "");
  for (const term of denyTerms.map(normalizeForPrivacy).filter((x) => x !== "")) {
    if (new RegExp(`(?<![a-z0-9])${escapeRe(term)}(?![a-z0-9])`).test(t)) return "contains a private term";
    const bare = term.replace(SEPARATORS, "");
    if (bare !== term && bare !== "" && joined.includes(bare)) return "contains a private term";
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

// Verbatim overlap (spec 5.5): a run of RUN_WORDS normalized words shared with a cited session is a quote, and so is a
// whole short turn (SHORT_MIN to RUN_WORDS - 1 words) found as a contiguous run in the candidate.
export const RUN_WORDS = 8;
export const SHORT_MIN = 4;
const wordsOf = (text: string): string[] => normalizeForPrivacy(text).split(/[^\p{L}\p{N}]+/u).filter((w) => w !== "");
export const runWords = (text: string): string => wordsOf(text).join(" ");

function runsOf(text: string): Set<string> {
  return runsOfWords(wordsOf(text));
}

function runsOfWords(w: readonly string[]): Set<string> {
  const out = new Set<string>();
  for (let i = 0; i + RUN_WORDS <= w.length; i++) out.add(w.slice(i, i + RUN_WORDS).join(" "));
  return out;
}

// One session's text as the verbatim layer compares it, computed once per session per publish run.
// `runs` come from the session's lines joined in order, so a quote that crosses from one turn into the next is a run
// too (sessions are never joined to each other); `short` holds each turn of SHORT_MIN to RUN_WORDS - 1 words, whole.
export interface SessionShingles {
  runs: ReadonlySet<string>;
  short: readonly string[];
}

export function shingleSession(lines: readonly string[]): SessionShingles {
  const perLine = lines.map(wordsOf);
  const short = perLine.filter((w) => w.length >= SHORT_MIN && w.length < RUN_WORDS).map((w) => w.join(" "));
  return { runs: runsOfWords(perLine.flat()), short: [...new Set(short)] };
}

// `isPublic(run)` says whether a run (or a whole short turn) already appears in the toolkit's own public text; those
// are not leaks.
export function copiedFrom(text: string, sessions: readonly SessionShingles[], isPublic: (run: string) => boolean): boolean {
  const padded = ` ${runWords(text)} `;
  const copiedRunHere = [...runsOf(text)].some((run) => sessions.some((s) => s.runs.has(run)) && !isPublic(run));
  return copiedRunHere || sessions.some((s) => s.short.some((turn) => padded.includes(` ${turn} `) && !isPublic(turn)));
}

const CORPUS_FILE_MAX = 1_000_000;

// The toolkit's public text, as normalized words: a run (or short turn) found here is public already (spec §5.5).
// "Public" is what the remote default branch holds (origin/<defaultBranch>), read from git objects, never the working
// tree: uncommitted, staged-only and unpushed text isn't public yet. Without that ref the corpus is empty (fail
// closed). Symlinks, binaries and files over 1 MB are skipped.
export async function toolkitCorpus(git: GitRunner, repo: string, defaultBranch: string): Promise<string> {
  const ref = `origin/${defaultBranch}`;
  const tree = await git.run(["ls-tree", "-r", "-l", "-z", ref], repo);
  if (!tree.ok) return "";
  const skip: string[] = [];
  for (const entry of tree.stdout.split("\0").filter((e) => e !== "")) {
    const tab = entry.indexOf("\t"); // <mode> <type> <object> <size>\t<path>
    const [mode, , , size] = entry.slice(0, tab).split(/ +/);
    if (mode === "120000" || Number(size) > CORPUS_FILE_MAX) skip.push(entry.slice(tab + 1));
  }
  // Every line of every text file at the ref, in one process; -I leaves binaries out. Exit 1 means no line at all.
  const grep = await git.run(["grep", "-I", "-z", "--full-name", "-e", "", ref, "--", ".", ...skip.map((f) => `:(exclude,literal)${f}`)], repo);
  if (!grep.ok) return "";
  const files = new Map<string, string[]>();
  for (const line of grep.stdout.split("\n")) {
    const cut = line.indexOf("\0"); // <ref>:<path>\0<line>
    if (cut < 0) continue;
    const lines = files.get(line.slice(0, cut));
    if (lines === undefined) files.set(line.slice(0, cut), [line.slice(cut + 1)]);
    else lines.push(line.slice(cut + 1));
  }
  // "|" is never a word, so a run can't span the end of one file and the start of the next.
  return ` ${[...files.values()].map((lines) => runWords(lines.join("\n"))).join(" | ")} `;
}
