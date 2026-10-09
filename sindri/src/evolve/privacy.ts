// The publication gate (spec amendment 2): nothing workplace-specific reaches the public repo.
const EMAIL = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/;
const HOME = /\/(?:Users|home)\/[A-Za-z0-9._-]+/;
const escapeRe = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

export function privacyProblem(text: string, denyTerms: readonly string[]): string | null {
  for (const t of denyTerms) {
    if (new RegExp(`(?<![A-Za-z0-9])${escapeRe(t)}(?![A-Za-z0-9])`, "i").test(text)) return "contains a private term";
  }
  return EMAIL.test(text) || HOME.test(text) ? "contains an email address or a home directory path" : null;
}
