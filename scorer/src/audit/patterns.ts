import type { HumanTurn } from "./human-turns.js";

export type PatternName = "ship_recipe" | "ci_conflicts" | "push_only" | "evidence_env" | "image_turn" | "handoff" | "restate" | "rigor" | "scope_surface" | "dispatch";

// Deterministic floor counts (spec appendix). A pattern feeds a metric only when `scorer audit --label`
// marks it metric-grade (Task 6). Workplace-specific labels never appear here; profiles add their own
// patterns in a later plan.
export const PATTERNS: Readonly<Record<PatternName, RegExp>> = {
  ship_recipe: /\/review\s*\+\s*\/addressReview|review loop|bugbot|raise (?:the|it|a) pr as (?:a )?draft|ready (?:to|for) review/i,
  ci_conflicts: /failing (?:ci|checks|tests|db tests|smoke)|fail(?:ed|ing)? ci|merge conflic|\bconflicts\b|typecheck|\blint/i,
  push_only: /^(?:push(?: it)?|push \w+|raise (?:a|the) pr|raise prs?|commit and push)[.! ]*$/i,
  evidence_env: /living preview|\/ui-evidence|pixel diff|visual parity|screenshots?\b.*\b(?:attach|issue|ticket)|attach (?:it|this|that|them|the \w+) to (?:the )?(?:issue|ticket|pr)/i,
  // Counts image attachments, not defects: on a labeled sample only 22 of 80 hits were real defects.
  image_turn: /\[Image #\d+\]/,
  handoff: /handoff|hand off|\bdigest\b|pick up (?:from|where)|\bcrashed\b|take ?over/i,
  restate: /as i (?:mentioned|said)|i (?:already|just) (?:said|told)|you were supposed to|why are we not doing it|(?:described|suggests?) (?:in )?(?:the )?ticket|ticket (?:body|description) (?:suggests|says|describes)/i,
  rigor: /100% sure|with evidence|actually (?:works|loads|replicate)|refute or confirm|are you sure|double check|tests? to (?:repro|replicate)/i,
  scope_surface: /everywhere|all (?:the )?(?:places|surfaces|screens|editors)|other (?:places|surfaces|code|screens)|\bwe forgot\b|did we (?:lose|update|miss)/i,
  dispatch: /https?:\/\/\S+\/issue\/[A-Z][A-Z0-9]{1,9}-\d+|\/bugFixOrchestrator\b/,
};

export function countPatterns(turns: Iterable<HumanTurn>): Record<PatternName, { turns: number; sessions: number }> {
  const names = Object.keys(PATTERNS) as PatternName[];
  const acc = Object.fromEntries(names.map((n) => [n, { turns: 0, sessions: new Set<string>() }])) as Record<PatternName, { turns: number; sessions: Set<string> }>;
  for (const t of turns) {
    if (t.kind !== "turn" && t.kind !== "command") continue;
    for (const n of names) {
      if (PATTERNS[n].test(t.text)) {
        acc[n].turns += 1;
        acc[n].sessions.add(t.session);
      }
    }
  }
  return Object.fromEntries(names.map((n) => [n, { turns: acc[n].turns, sessions: acc[n].sessions.size }])) as Record<PatternName, { turns: number; sessions: number }>;
}
