import { stripInvisible, WHITESPACE } from "./invisible.js";

// The publication gate (spec amendment 2): nothing workplace-specific reaches the public repo.
// Matching runs on a normalized copy, so a term can't be hidden behind a line break, a double space,
// an NBSP, a zero-width or soft-hyphen character, a quote prefix, fullwidth letters or a decomposed accent.
const EMAIL = /[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}/;
// An absolute home directory with a user segment. Case-sensitive on purpose: "/users/me" and "github.com/users/foo" are routes, not homes.
const HOME_UNIX = /(?<![A-Za-z0-9._/-])\/(?:Users|home)\/[A-Za-z0-9._-]+/;
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

export function privacyProblem(text: string, denyTerms: readonly string[]): string | null {
  const t = normalizeForPrivacy(text);
  for (const term of denyTerms.map(normalizeForPrivacy).filter((x) => x !== "")) {
    if (new RegExp(`(?<![a-z0-9])${escapeRe(term)}(?![a-z0-9])`).test(t)) return "contains a private term";
  }
  return EMAIL.test(t) || HOME_UNIX.test(visibleForm(text)) || HOME_WINDOWS.test(t) ? "contains an email address or a home directory path" : null;
}
