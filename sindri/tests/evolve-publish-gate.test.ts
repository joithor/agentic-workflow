import { describe, expect, it } from "vitest";

import { HOLD_LAYERS } from "../src/evolve/privacy.js";
import { GATE_LAYERS, runGate, verbatimChecker, type GateCtx } from "../src/evolve/publish-gate.js";
import { makeScrubber } from "../src/scrub/scrub.js";

const COPIED = "the overnight rota swap left two shifts without a charge nurse";
const line = (t: string) => ({ text: t, turn: t });

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
    expect(await runGate({ text: "Acme Care saw GLX-12 in quarbleScheduler", sessions: [] }, g)).toEqual({ layer: "terms", why: "contains a private term" });
    expect(copiesCalled).toBe(false); // a terms hit never reaches the verbatim layer
    expect(await runGate({ text: "seen on glx-12", sessions: [] }, g)).toEqual({ layer: "patterns", why: "matches a privacy.denyPatterns entry" });
    expect(await runGate({ text: "touches QuarbleScheduler", sessions: [] }, g)).toEqual({ layer: "identifiers", why: "contains a private-repo identifier" });
    expect(await runGate({ text: "a plain idea", sessions: ["s"] }, g)).toEqual({ layer: "verbatim", why: "copies transcript text" });
    expect(await runGate({ text: "a plain idea", sessions: ["s"] }, ctx({ copies: async () => "missing" })))
      .toEqual({ layer: "verbatim", why: "evidence transcripts are missing, so copied text can't be checked" });
    expect(await runGate({ text: "mail ops@example.org", sessions: [] }, ctx())).toEqual({ layer: "personal", why: "contains an email address or a home directory path" });
    expect(await runGate({ text: "a plain idea", sessions: [] }, ctx())).toBeNull();
  });
});

describe("verbatimChecker", () => {
  it("compares only the cited sessions that exist, and reads the corpus once, only after a candidate shares a run", async () => {
    let reads = 0;
    const sessions = new Map([["s1", new Map([[2, line("")], [1, line(COPIED)]])]]);
    const copies = verbatimChecker(sessions, async () => { reads++; return " | "; });
    expect(await copies("a plain idea with nothing copied from anywhere at all", ["s1", "s1", "gone"])).toBeNull();
    expect(reads).toBe(0);
    expect(await copies(`Fix: ${COPIED}`, ["s1", "gone"])).toBe("copies"); // partial resolution: the resolved one is checked
    expect(await copies(`Again: ${COPIED}`, ["s1"])).toBe("copies");
    expect(reads).toBe(1);
  });

  it("says missing when the candidate cites sessions and none resolves; nothing cited is not missing (security-sentinel-1)", async () => {
    const copies = verbatimChecker(new Map([["s1", new Map([[1, line(COPIED)]])]]), async () => " | ");
    expect(await copies(`Fix: ${COPIED}`, ["gone"])).toBe("missing");
    expect(await copies("a plain idea", ["gone", "also-gone"])).toBe("missing");
    expect(await copies(`Fix: ${COPIED}`, [])).toBeNull();
  });

  it("passes a run the corpus already holds", async () => {
    const copies = verbatimChecker(new Map([["s1", new Map([[1, line(COPIED)]])]]), async () => ` ${COPIED} `);
    expect(await copies(`Fix: ${COPIED}`, ["s1"])).toBeNull();
  });
});
