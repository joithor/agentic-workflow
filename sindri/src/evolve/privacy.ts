// The publication gate (spec amendment 2): nothing workplace-specific reaches the public repo.
// Matching runs on a normalized copy, so a term can't be hidden behind a line break, a double space,
// an NBSP, a zero-width or soft-hyphen character, a quote prefix, fullwidth letters or a decomposed accent.
const EMAIL = /[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}/;
const HOME = /\/(?:users|home)\/[a-z0-9._-]+|[a-z]:\\+users\\+[a-z0-9._-]+|(?<![a-z0-9])~\//;
const INVISIBLE = /[­͏؜᠎​-‏‪-‮⁠-⁯︀-️﻿]/g;
const escapeRe = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

export function normalizeForPrivacy(text: string): string {
  return text
    .normalize("NFKC")
    .replace(INVISIBLE, "")
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
  return EMAIL.test(t) || HOME.test(t) ? "contains an email address or a home directory path" : null;
}
