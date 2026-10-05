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

// Redact string values (and object keys) before serializing: in serialized
// JSON a newline or tab is a backslash plus a letter, which hides the word
// boundary the patterns anchor on.
export function redactDeep(value: unknown): unknown {
  if (typeof value === "string") return redactSecrets(value);
  if (Array.isArray(value)) return value.map(redactDeep);
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [redactSecrets(k), redactDeep(v)]));
  }
  return value;
}

// Callers that store untrusted input must redact (redactDeep) first, then cap: a secret cut
// at the cap boundary would no longer match its pattern.
export function capText(text: string, maxChars: number): string {
  if (text.length <= maxChars) return text;
  return `${text.slice(0, maxChars)}[truncated ${text.length - maxChars} chars]`;
}

export function capJson(value: unknown, maxChars: number): string {
  return capText(JSON.stringify(value), maxChars);
}

type Slot = { holder: Record<string, unknown> | unknown[]; key: string | number };

function stringSlots(value: unknown, out: Slot[]): void {
  if (Array.isArray(value)) {
    value.forEach((v, i) => (typeof v === "string" ? out.push({ holder: value, key: i }) : stringSlots(v, out)));
  } else if (value !== null && typeof value === "object") {
    const rec = value as Record<string, unknown>;
    for (const k of Object.keys(rec)) {
      if (typeof rec[k] === "string") out.push({ holder: rec, key: k });
      else stringSlots(rec[k], out);
    }
  }
}

const SHRINK_FLOOR = 64;

// Like capJson, but the result always parses as JSON: instead of slicing the
// serialization, the longest string values are shortened (each gets its own
// "[truncated N chars]" marker) until the whole thing fits. Redact first.
export function capJsonValue(value: unknown, maxChars: number): string {
  let text = JSON.stringify(value);
  if (text.length <= maxChars) return text;
  const work = structuredClone(value);
  while (text.length > maxChars) {
    const slots: Slot[] = [];
    stringSlots(work, slots);
    const at = (s: Slot): string => (s.holder as Record<string | number, string>)[s.key];
    const longest = slots.reduce<Slot | null>((best, s) => (best === null || at(s).length > at(best).length ? s : best), null);
    if (longest === null || at(longest).length <= SHRINK_FLOOR) {
      // Nothing left to shorten (e.g. a huge array of numbers): keep a valid stub.
      return JSON.stringify({ truncated: capText(text, Math.max(0, maxChars - 80)) });
    }
    const current = at(longest);
    const keep = Math.max(0, current.length - (text.length - maxChars) - SHRINK_FLOOR);
    (longest.holder as Record<string | number, string>)[longest.key] = capText(current, keep);
    text = JSON.stringify(work);
  }
  return text;
}
