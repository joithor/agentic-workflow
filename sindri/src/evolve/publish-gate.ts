import type { Scrubber } from "../scrub/scrub.js";
import { identifierProblem } from "./privacy-derived.js";
import { copiedFrom, patternProblem, personalProblem, shingleSession, termProblem, type HoldLayer, type SessionShingles } from "./privacy.js";
import type { SessionFile } from "./transcripts.js";

// The publish gate (privacy spec §5.4 to §5.6) as one ordered list of layer checks. `generalize` (a proposal with no
// public version) is decided before the gate runs, so the list starts at `terms`.
export interface Candidate {
  text: string; // the public version's raw fields plus its rendered task
  // The session prefixes the verbatim layer checks: those its transcript evidence cites plus the source sessions
  // recorded when it was saved (security-sentinel-0); the model chooses the first, code the second.
  sessions: readonly string[];
  // The source sessions code recorded when it was saved (NO_TRANSCRIPT left out), also among `sessions`. Unlike a cited
  // ref, which may point into another repo, each of these was read from this repo's transcripts, so one that no longer
  // resolves is text the model saw that can't be checked any more: the verbatim layer holds (run 4 security-sentinel-1).
  recorded: readonly string[];
  // Its source fed transcript text to the model but no source session was recorded (a proposal saved before
  // provenance existed): copied text can't be checked, so the verbatim layer holds (fail closed).
  unrecorded: boolean;
}

// "copies": a run or a whole short turn of a checked session is in the text; "source-missing": a recorded source session
// doesn't resolve (cleaned up after the host's retention period, moved); "missing": the candidate has at least one
// session to check and none of them resolves (deleted, moved, another transcripts dir). Either way copied text can't
// be checked (fail closed).
export type VerbatimResult = "copies" | "source-missing" | "missing" | null;
const VERBATIM_WHY: Record<Exclude<VerbatimResult, null>, string> = {
  copies: "copies transcript text",
  "source-missing": "a source transcript it was built from is missing, so copied text can't be checked",
  missing: "evidence transcripts are missing, so copied text can't be checked",
};

export interface GateCtx {
  deny: readonly string[];
  patterns: readonly string[];
  derived: ReadonlySet<string>;
  user: string;
  scrubber: Scrubber;
  copies: (text: string, sessions: readonly string[], recorded: readonly string[]) => Promise<VerbatimResult>;
}

type GateLayer = { layer: Exclude<HoldLayer, "generalize">; check: (c: Candidate, g: GateCtx) => Promise<string | null> };

export const GATE_LAYERS: readonly GateLayer[] = [
  { layer: "terms", check: async (c, g) => termProblem(c.text, g.deny) },
  { layer: "patterns", check: async (c, g) => patternProblem(c.text, g.patterns) },
  { layer: "identifiers", check: async (c, g) => identifierProblem(c.text, g.derived) },
  { layer: "verbatim", check: async (c, g) => {
    if (c.unrecorded) return "its source transcripts weren't recorded, so copied text can't be checked";
    const r = await g.copies(c.text, c.sessions, c.recorded);
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

// The verbatim layer's evidence for one publish run: each session file is shingled once, on first use, and the toolkit
// corpus is read at most once, only when some candidate shares a run with its evidence. A session prefix resolves to
// every file that has it (an ambiguous prefix brings in all of them, fail closed), each shingled on its own so runs
// never cross files. Some cited sessions missing is fine (the ones that resolve are checked; Review Focus 1); all of them
// missing is "missing". A recorded source session must resolve, or it is "source-missing" (run 4 security-sentinel-1).
export function verbatimChecker(
  files: ReadonlyMap<string, SessionFile>, corpus: () => Promise<string>,
): (text: string, sessions: readonly string[], recorded: readonly string[]) => Promise<VerbatimResult> {
  const shingled = new Map<string, SessionShingles>();
  const byPrefix = new Map<string, string[]>();
  for (const [file, f] of files) byPrefix.set(f.session, [...(byPrefix.get(f.session) ?? []), file]);
  let pub: Promise<string> | null = null;
  const of = (file: string): SessionShingles => {
    const known = shingled.get(file);
    if (known !== undefined) return known;
    const made = shingleSession([...files.get(file)!.lines].sort(([a], [b]) => a - b).map(([, t]) => t));
    shingled.set(file, made);
    return made;
  };
  return async (text, sessions, recorded) => {
    if (recorded.some((s) => !byPrefix.has(s))) return "source-missing";
    const asked = [...new Set(sessions)];
    const mine = asked.flatMap((s) => (byPrefix.get(s) ?? []).map(of));
    if (asked.length > 0 && mine.length === 0) return "missing";
    if (!copiedFrom(text, mine, () => false)) return null;
    pub ??= corpus();
    const c = await pub;
    return copiedFrom(text, mine, (run) => c.includes(` ${run} `)) ? "copies" : null;
  };
}
