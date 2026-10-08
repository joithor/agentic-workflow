import type { WorkItem } from "../adapters/types.js";
import { sizeRank, type Profile, type Size } from "../profile/schema.js";

// Deterministic sizing for plan tasks (spec amendment 2). Model triage replaces
// it in rollout step 2; sizedBy records which one produced the size.
const LIMITS: readonly [Size, number, number][] = [
  ["XS", 1, 40],
  ["S", 3, 200],
  ["M", 6, 500],
  ["L", 10, 1000],
];

export function sizeByRules(files: number, codeLines: number): Size {
  return LIMITS.find(([, f, c]) => files <= f && codeLines <= c)?.[0] ?? "XL";
}

export interface Assessment {
  size: Size | null; // null: the task has no Files block and no code, so rules can't size it
  sizedBy: "rules";
  ambiguity: "none" | "unknown";
  trusted: boolean;
}

const num = (item: WorkItem, key: string): number => Number(item.meta[key] ?? 0);

// Items without an order sort after ordered ones, never as NaN.
export const orderOf = (item: WorkItem): number => item.order ?? Number.MAX_SAFE_INTEGER;

export function assess(item: WorkItem, profile: Profile): Assessment {
  const clear = num(item, "hasFilesBlock") === 1 && num(item, "codeLines") > 0 && (item.steps?.total ?? 0) > 0;
  const sizable = num(item, "hasFilesBlock") === 1 || num(item, "codeLines") > 0;
  return {
    size: sizable ? sizeByRules(num(item, "files"), num(item, "codeLines")) : null,
    sizedBy: "rules",
    ambiguity: clear ? "none" : "unknown",
    // Spec §8.3: every author must be trusted; an item with no known author is not.
    trusted: item.authors.length > 0 && item.authors.every((a) => profile.trustedAuthors.includes(a.id)),
  };
}

// Plan tasks depend on the tasks before them, so only the first open task of
// each plan is startable. Items without a plan stand alone.
export function nextPerPlan(items: WorkItem[]): Set<string> {
  const first = new Map<string, WorkItem>();
  for (const item of items) {
    if (item.state !== "open") continue;
    const plan = String(item.meta.plan ?? item.id);
    const seen = first.get(plan);
    if (seen === undefined || orderOf(item) < orderOf(seen)) first.set(plan, item);
  }
  return new Set([...first.values()].map((i) => i.id));
}

// Why auto-small would not start this item, or null when it would (shown as WOULD-START).
export function startBlocker(item: WorkItem, a: Assessment, isNext: boolean, limit: Size): string | null {
  if (item.state !== "open") return "done";
  if (!isNext) return "waits on earlier task";
  if (a.size === null) return "unsized";
  if (sizeRank(a.size) > sizeRank(limit)) return `size > ${limit}`;
  if (a.ambiguity !== "none") return "unclear";
  if (!a.trusted) return "untrusted author";
  return null;
}
