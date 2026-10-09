import { stripInvisible, WHITESPACE } from "./invisible.js";

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

// `osUser` is the current OS user (from the system seam): their home path is held in any case and in Claude's encoding.
export function privacyProblem(text: string, denyTerms: readonly string[], osUser = ""): string | null {
  const t = normalizeForPrivacy(text);
  const visible = visibleForm(text);
  const me = osUser === "" ? null : escapeRe(osUser.normalize("NFKC"));
  const mine = me === null ? false : new RegExp(`(?<![A-Za-z0-9._-])\\/(?:users|home)\\/${me}(?![A-Za-z0-9._-])|(?<![A-Za-z0-9])-(?:users|home)-${me}(?![A-Za-z0-9])`, "i").test(visible);
  for (const term of denyTerms.map(normalizeForPrivacy).filter((x) => x !== "")) {
    if (new RegExp(`(?<![a-z0-9])${escapeRe(term)}(?![a-z0-9])`).test(t)) return "contains a private term";
  }
  return EMAIL.test(t) || HOME_UNIX.test(visible) || CLAUDE_DIR.test(visible) || mine || HOME_WINDOWS.test(t) ? "contains an email address or a home directory path" : null;
}
