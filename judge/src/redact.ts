// Stored judge inputs (decision_details, eval_items) can carry brief text,
// diffs and transcript slices. Patterns follow Navigator's judge.redact_secrets
// (hooks/nav_hook_lib/judge.py); the 40+ hex rule only matches standalone runs,
// so short commit shas in prose survive (full 40-char git shas are redacted too).
const PATTERNS: readonly RegExp[] = [
  /\bsk-[A-Za-z0-9_-]{20,}/g,
  /\bgh[pousr]_[A-Za-z0-9]{30,}/g,
  /\bAKIA[A-Z0-9]{16}\b/g,
  /\bxox[abprs]-[A-Za-z0-9-]{10,}/g,
  /\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}/g,
  /\b[0-9a-f]{40,}\b/gi,
];

export const INPUT_CAP = 16000;

export function redactSecrets(text: string): string {
  return PATTERNS.reduce((acc, re) => acc.replace(re, "[REDACTED]"), text);
}

// Callers that store untrusted text must redact first, then cap: a secret cut
// at the cap boundary would no longer match its pattern.
export function capText(text: string, maxChars: number): string {
  if (text.length <= maxChars) return text;
  return `${text.slice(0, maxChars)}[truncated ${text.length - maxChars} chars]`;
}

export function capJson(value: unknown, maxChars: number): string {
  return capText(JSON.stringify(value), maxChars);
}
