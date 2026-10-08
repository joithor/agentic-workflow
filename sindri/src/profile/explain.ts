import type { LoadedProfile } from "./load.js";

function get(value: unknown, segs: string[]): unknown {
  let cur = value;
  for (const s of segs) {
    if (cur === null || typeof cur !== "object" || !(s in cur)) return undefined;
    cur = (cur as Record<string, unknown>)[s];
  }
  return cur;
}

// Precedence (spec §11.1): repo file > profile file > core default.
export function explainKey(loaded: LoadedProfile, key: string, repo?: string): { key: string; value: unknown; source: string } | null {
  const segs = key.split(".");
  if (repo !== undefined) {
    const value = get(loaded.repos[repo]?.overrides, segs);
    if (value !== undefined) return { key, value, source: `repos/${repo}.yaml (overrides)` };
  }
  const value = get(loaded.profile, segs);
  if (value === undefined) return null;
  return { key, value, source: get(loaded.raw.profile, segs) !== undefined ? "profile.yaml" : "default" };
}
