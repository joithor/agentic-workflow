// A click/fill target is a bare test id, `role:<role>|<accessible name>` or `text:<text>`.
// Pure parsing lives here so the schema, the write guard and the executor agree on one grammar.
export type StepTarget =
  | { kind: "testId"; testId: string }
  | { kind: "role"; role: string; name: string }
  | { kind: "text"; text: string };

const ROLE_PREFIX = "role:";
const TEXT_PREFIX = "text:";

export const isPrefixedTarget = (target: string): boolean => target.startsWith(ROLE_PREFIX) || target.startsWith(TEXT_PREFIX);

export function parseStepTarget(target: string): StepTarget | { error: string } {
  // "Role:button|Save" or " text:Save" would silently become a test id and miss: reject the near-miss.
  if (!isPrefixedTarget(target) && /^\s*(role|text):/i.test(target)) return { error: `target prefix must be exactly lower-case "role:" or "text:" with no leading space: ${target}` };
  if (target.startsWith(ROLE_PREFIX)) {
    const rest = target.slice(ROLE_PREFIX.length);
    const bar = rest.indexOf("|");
    const role = bar < 0 ? "" : rest.slice(0, bar).trim();
    const name = bar < 0 ? "" : rest.slice(bar + 1);
    return !/^[a-z]+$/.test(role) || name.trim() === "" ? { error: `role target must be "role:<lower-case role>|<accessible name>": ${target}` } : { kind: "role", role, name };
  }
  if (target.startsWith(TEXT_PREFIX)) {
    const text = target.slice(TEXT_PREFIX.length);
    return text.trim() === "" ? { error: `text target must be "text:<text>": ${target}` } : { kind: "text", text };
  }
  return { kind: "testId", testId: target };
}

/** The human-readable part of a target the write guard should read: a test id, an accessible name or a text. */
export function targetLabel(target: string): string {
  const parsed = parseStepTarget(target);
  if ("error" in parsed) return target;
  return parsed.kind === "role" ? parsed.name : parsed.kind === "text" ? parsed.text : parsed.testId;
}
