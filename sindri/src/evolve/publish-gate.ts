import type { Scrubber } from "../scrub/scrub.js";
import { identifierProblem } from "./privacy-derived.js";
import { copiedFrom, patternProblem, personalProblem, shingleSession, termProblem, type HoldLayer, type SessionShingles } from "./privacy.js";
import type { SessionText } from "./transcripts.js";

// The publish gate (privacy spec §5.4 to §5.6) as one ordered list of layer checks. `generalize` (a proposal with no
// public version) is decided before the gate runs, so the list starts at `terms`.
export interface Candidate {
  text: string; // the public version's raw fields plus its rendered task
  sessions: readonly string[]; // the session prefixes its transcript evidence cites
}

// "copies": a run or a whole short turn of a cited session is in the text; "missing": the candidate cites at least one
// session and none of them resolves (deleted, moved, another transcripts dir), so copied text can't be checked (fail closed).
export type VerbatimResult = "copies" | "missing" | null;
const VERBATIM_WHY: Record<Exclude<VerbatimResult, null>, string> = {
  copies: "copies transcript text",
  missing: "evidence transcripts are missing, so copied text can't be checked",
};

export interface GateCtx {
  deny: readonly string[];
  patterns: readonly string[];
  derived: ReadonlySet<string>;
  user: string;
  scrubber: Scrubber;
  copies: (text: string, sessions: readonly string[]) => Promise<VerbatimResult>;
}

type GateLayer = { layer: Exclude<HoldLayer, "generalize">; check: (c: Candidate, g: GateCtx) => Promise<string | null> };

export const GATE_LAYERS: readonly GateLayer[] = [
  { layer: "terms", check: async (c, g) => termProblem(c.text, g.deny) },
  { layer: "patterns", check: async (c, g) => patternProblem(c.text, g.patterns) },
  { layer: "identifiers", check: async (c, g) => identifierProblem(c.text, g.derived) },
  { layer: "verbatim", check: async (c, g) => {
    const r = await g.copies(c.text, c.sessions);
    return r === null ? null : VERBATIM_WHY[r];
  } },
  { layer: "personal", check: async (c, g) => personalProblem(c.text, g.user) },
  { layer: "scrubber", check: async (c, g) => (g.scrubber.find(c.text).length > 0 ? "contains text the scrubber redacts (a secret or a scrub.extraPatterns match)" : null) },
];

// In order; the first hit holds and the rest don't run (a terms hit never reads the toolkit corpus). The reason names
// the layer's check, never the matched text.
export async function runGate(c: Candidate, g: GateCtx): Promise<{ layer: HoldLayer; why: string } | null> {
  for (const l of GATE_LAYERS) {
    const why = await l.check(c, g);
    if (why !== null) return { layer: l.layer, why };
  }
  return null;
}

// The verbatim layer's evidence for one publish run: each cited session is shingled once, on first use, and the toolkit
// corpus is read at most once, only when some candidate shares a run with its evidence. Some cited sessions missing is
// fine (the ones that resolve are checked); all of them missing is "missing".
export function verbatimChecker(
  sessions: ReadonlyMap<string, ReadonlyMap<number, SessionText>>, corpus: () => Promise<string>,
): (text: string, cited: readonly string[]) => Promise<VerbatimResult> {
  const shingled = new Map<string, SessionShingles>();
  let pub: Promise<string> | null = null;
  const of = (session: string, lines: ReadonlyMap<number, SessionText>): SessionShingles => {
    const known = shingled.get(session);
    if (known !== undefined) return known;
    const made = shingleSession([...lines].sort(([a], [b]) => a - b).map(([, t]) => t));
    shingled.set(session, made);
    return made;
  };
  return async (text, cited) => {
    const asked = [...new Set(cited)];
    const mine = asked.flatMap((s) => {
      const lines = sessions.get(s);
      return lines === undefined ? [] : [of(s, lines)];
    });
    if (asked.length > 0 && mine.length === 0) return "missing";
    if (!copiedFrom(text, mine, () => false)) return null;
    pub ??= corpus();
    const c = await pub;
    return copiedFrom(text, mine, (run) => c.includes(` ${run} `)) ? "copies" : null;
  };
}
