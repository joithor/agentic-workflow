import { describe, expect, it } from "vitest";

import { HOLD_LAYERS } from "../src/evolve/privacy.js";
import { GATE_LAYERS, runGate, verbatimChecker, type GateCtx } from "../src/evolve/publish-gate.js";
import { makeScrubber } from "../src/scrub/scrub.js";

const COPIED = "the overnight rota swap left two shifts without a charge nurse";
const line = (t: string) => ({ text: t, turn: t });
// Session files keyed by their full identity (kieran-typescript-reviewer-3); several can share one prefix.
const files = (...fs: [session: string, lines: Map<number, ReturnType<typeof line>>][]) => new Map(fs.map(([session, lines], i) => [`/t/f${i}.jsonl`, { session, lines }]));

const ctx = (over: Partial<GateCtx> = {}): GateCtx => ({
  deny: ["Acme Care"], patterns: ["GLX-\\d+"], derived: new Set(["quarblescheduler"]), user: "", scrubber: makeScrubber(),
  copies: async () => null, ...over,
});

describe("the publish gate as an ordered list (architecture-strategist-7)", () => {
  it("runs the layers in the documented order, after generalize", () => {
    expect(GATE_LAYERS.map((l) => l.layer)).toEqual(HOLD_LAYERS.slice(1));
  });

  it("returns the first hit only, and passes clean text", async () => {
    let copiesCalled = false;
    const g = ctx({ copies: async () => { copiesCalled = true; return "copies"; } });
    expect(await runGate({ text: "Acme Care saw GLX-12 in quarbleScheduler", sessions: [], unrecorded: false, recorded: [] }, g)).toEqual({ layer: "terms", why: "contains a private term" });
    expect(copiesCalled).toBe(false); // a terms hit never reaches the verbatim layer
    expect(await runGate({ text: "seen on glx-12", sessions: [], unrecorded: false, recorded: [] }, g)).toEqual({ layer: "patterns", why: "matches a privacy.denyPatterns entry" });
    expect(await runGate({ text: "touches QuarbleScheduler", sessions: [], unrecorded: false, recorded: [] }, g)).toEqual({ layer: "identifiers", why: "contains a private-repo identifier" });
    expect(await runGate({ text: "a plain idea", sessions: ["s"], unrecorded: false, recorded: [] }, g)).toEqual({ layer: "verbatim", why: "copies transcript text" });
    expect(await runGate({ text: "a plain idea", sessions: ["s"], unrecorded: false, recorded: [] }, ctx({ copies: async () => "missing" })))
      .toEqual({ layer: "verbatim", why: "evidence transcripts are missing, so copied text can't be checked" });
    expect(await runGate({ text: "a plain idea", sessions: ["s"], unrecorded: false, recorded: ["s"] }, ctx({ copies: async () => "source-missing" })))
      .toEqual({ layer: "verbatim", why: "a source transcript it was built from is missing, so copied text can't be checked" });
    expect(await runGate({ text: "mail ops@example.org", sessions: [], unrecorded: false, recorded: [] }, ctx())).toEqual({ layer: "personal", why: "contains an email address or a home directory path" });
    expect(await runGate({ text: "a plain idea", sessions: [], unrecorded: false, recorded: [] }, ctx())).toBeNull();
  });

  it("holds a transcript-sourced candidate whose source sessions weren't recorded at the verbatim layer, without reading transcripts (security-sentinel-0)", async () => {
    let copiesCalled = false;
    const g = ctx({ copies: async () => { copiesCalled = true; return null; } });
    expect(await runGate({ text: "a plain idea", sessions: ["s"], unrecorded: true, recorded: [] }, g))
      .toEqual({ layer: "verbatim", why: "its source transcripts weren't recorded, so copied text can't be checked" });
    expect(copiesCalled).toBe(false);
    // the earlier layers still come first
    expect(await runGate({ text: "Acme Care idea", sessions: [], unrecorded: true, recorded: [] }, g)).toEqual({ layer: "terms", why: "contains a private term" });
  });
});

describe("verbatimChecker", () => {
  it("compares only the cited sessions that exist, and reads the corpus once, only after a candidate shares a run", async () => {
    let reads = 0;
    const sessions = files(["s1", new Map([[2, line("")], [1, line(COPIED)]])]);
    const copies = verbatimChecker(sessions, async () => { reads++; return " | "; });
    expect(await copies("a plain idea with nothing copied from anywhere at all", ["s1", "s1", "gone"], [])).toBeNull();
    expect(reads).toBe(0);
    expect(await copies(`Fix: ${COPIED}`, ["s1", "gone"], [])).toBe("copies"); // partial resolution: the resolved one is checked
    expect(await copies(`Again: ${COPIED}`, ["s1"], [])).toBe("copies");
    expect(reads).toBe(1);
  });

  it("says missing when the candidate cites sessions and none resolves; nothing cited is not missing (security-sentinel-1)", async () => {
    const copies = verbatimChecker(files(["s1", new Map([[1, line(COPIED)]])]), async () => " | ");
    expect(await copies(`Fix: ${COPIED}`, ["gone"], [])).toBe("missing");
    expect(await copies("a plain idea", ["gone", "also-gone"], [])).toBe("missing");
    expect(await copies(`Fix: ${COPIED}`, [], [])).toBeNull();
  });

  it("requires every recorded source session to resolve, while a cited one may be missing (run 4 security-sentinel-1)", async () => {
    const copies = verbatimChecker(files(["s1", new Map([[1, line(COPIED)]])], ["s2", new Map([[1, line("an unrelated line")]])]), async () => " | ");
    expect(await copies("a plain idea", ["gone", "s2"], ["gone", "s2"])).toBe("source-missing"); // a recorded source is gone
    expect(await copies(`Fix: ${COPIED}`, ["s1", "gone"], ["gone"])).toBe("source-missing"); // even when the copy is found elsewhere
    expect(await copies("a plain idea", ["gone", "s2"], ["s2"])).toBeNull(); // only a cited session is gone: tolerated (Review Focus 1)
    expect(await copies(`Fix: ${COPIED}`, ["s1", "s2"], ["s1", "s2"])).toBe("copies");
    expect(await copies("a plain idea", ["gone"], ["gone"])).toBe("source-missing"); // the more specific reason wins over missing
  });

  it("passes a run the corpus already holds", async () => {
    const copies = verbatimChecker(files(["s1", new Map([[1, line(COPIED)]])]), async () => ` ${COPIED} `);
    expect(await copies(`Fix: ${COPIED}`, ["s1"], [])).toBeNull();
  });

  it("checks every file that shares a cited prefix, each on its own: same line numbers never overwrite each other (kieran-typescript-reviewer-3)", async () => {
    const other = "an unrelated first line about something else entirely here today";
    const copies = verbatimChecker(files(["s1", new Map([[1, line(COPIED)]])], ["s1", new Map([[1, line(other)]])]), async () => " | ");
    expect(await copies(`Fix: ${COPIED}`, ["s1"], [])).toBe("copies");
    expect(await copies(`Also: ${other}`, ["s1"], [])).toBe("copies");
    // runs never cross from one file into the other
    expect(await copies(`${COPIED.split(" ").slice(-4).join(" ")} ${other.split(" ").slice(0, 4).join(" ")}`, ["s1"], [])).toBeNull();
  });
});
