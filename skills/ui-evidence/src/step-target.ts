// A click/fill target is a bare test id, `role:<role>|<accessible name>` or `text:<text>`.
// Pure parsing lives here so the schema, the write guard and the executor agree on one grammar.
export type StepTarget =
  | { kind: "testId"; testId: string }
  | { kind: "role"; role: string; name: string; nth?: number }
  | { kind: "text"; text: string; nth?: number };

const ROLE_PREFIX = "role:";
const TEXT_PREFIX = "text:";

export const isPrefixedTarget = (target: string): boolean => target.startsWith(ROLE_PREFIX) || target.startsWith(TEXT_PREFIX);

/** A trailing `#<n>` (1-based) picks the nth of several matches; without it exactly one must match. */
function splitNth(label: string): { label: string; nth?: number } | { error: string } {
  // "Shift #12" (space before #) is a plain name; the index must hug the name: "Day#2".
  const m = /^(.*\S)#(\d+)$/s.exec(label);
  if (m === null) return { label };
  const nth = Number(m[2]);
  return !Number.isSafeInteger(nth) || nth < 1 ? { error: `#<n> must be a 1-based integer: ${label}` } : { label: m[1] as string, nth };
}

export function parseStepTarget(target: string): StepTarget | { error: string } {
  // "Role:button|Save" or " text:Save" would silently become a test id and miss: reject the near-miss.
  if (!isPrefixedTarget(target) && /^\s*(role|text):/i.test(target)) return { error: `target prefix must be exactly lower-case "role:" or "text:" with no leading space: ${target}` };
  if (target.startsWith(ROLE_PREFIX)) {
    const rest = target.slice(ROLE_PREFIX.length);
    const bar = rest.indexOf("|");
    const role = bar < 0 ? "" : rest.slice(0, bar).trim();
    const split = splitNth(bar < 0 ? "" : rest.slice(bar + 1));
    if ("error" in split) return split;
    return !/^[a-z]+$/.test(role) || split.label.trim() === "" ? { error: `role target must be "role:<lower-case role>|<accessible name>[#<n>]": ${target}` } : { kind: "role", role, name: split.label, ...(split.nth === undefined ? {} : { nth: split.nth }) };
  }
  if (target.startsWith(TEXT_PREFIX)) {
    const split = splitNth(target.slice(TEXT_PREFIX.length));
    if ("error" in split) return split;
    return split.label.trim() === "" ? { error: `text target must be "text:<text>[#<n>]": ${target}` } : { kind: "text", text: split.label, ...(split.nth === undefined ? {} : { nth: split.nth }) };
  }
  return { kind: "testId", testId: target };
}

/** The human-readable part of a target the write guard should read: a test id, an accessible name or a text. */
export function targetLabel(target: string): string {
  const parsed = parseStepTarget(target);
  if ("error" in parsed) return target;
  return parsed.kind === "role" ? parsed.name : parsed.kind === "text" ? parsed.text : parsed.testId;
}
