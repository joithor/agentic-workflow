import { describe, expect, it } from "vitest";

import { privacyProblem } from "../src/evolve/privacy.js";

describe("privacyProblem (Review Focus 6)", () => {
  it("finds profile deny terms as whole words, case-insensitively, without echoing them", () => {
    expect(privacyProblem("Fix the ACME care scheduler", ["acme care"])).toBe("contains a private term");
    expect(privacyProblem("Fix the Acme_Care scheduler", ["Acme"])).toBe("contains a private term"); // an underscore separates words
    expect(privacyProblem("the Acmeville depot", ["Acme"])).toBeNull(); // not a whole word
    expect(privacyProblem("the (Acme) team", ["Acme"])).toBe("contains a private term");
    expect(privacyProblem("a+b is special", ["a+b"])).toBe("contains a private term");
    expect(privacyProblem("nothing here", ["Acme", "Globex"])).toBeNull();
  });

  it("finds emails and home directory paths even with no deny terms", () => {
    expect(privacyProblem("mail joi@example.com please", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("see /Users/joi/work/x.ts", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("see /home/builder/x.ts", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("a plain sentence about done-gate.sh and src/a.ts", [])).toBeNull();
  });
});

describe("privacyProblem: hidden and look-alike spellings (Task 10 fix round 1, I1 + m3)", () => {
  const HELD = "contains a private term";
  it("holds a multi-word term split by a line break, extra spaces, an NBSP, U+2028 or a quote prefix", () => {
    for (const hidden of ["Acme\nCare", "Acme  Care", "Acme\u00a0Care", "Acme\u2028Care", "Acme\r\nCare", "Acme\u0085Care", "Acme\t \u3000Care", "Acme\n> Care"]) {
      expect(privacyProblem(`We saw this at ${hidden} twice`, ["Acme Care"]), JSON.stringify(hidden)).toBe(HELD);
    }
  });

  it("holds fullwidth, decomposed and invisibly-padded spellings, and the Kelvin sign", () => {
    expect(privacyProblem("at \uff21\uff43\uff4d\uff45 today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("a Cafe\u0301 on the corner", ["Caf\u00e9"])).toBe(HELD);
    expect(privacyProblem("a Caf\u00e9 on the corner", ["Cafe\u0301"])).toBe(HELD);
    expect(privacyProblem("at Ac\u200bme Care", ["Acme Care"])).toBe(HELD);
    expect(privacyProblem("at Ac\u00adme\u2060 Care", ["Acme Care"])).toBe(HELD);
    expect(privacyProblem("at A\u202ecme", ["Acme"])).toBe(HELD);
    expect(privacyProblem("a \u212a-team", ["K-team"])).toBe(HELD);
    expect(privacyProblem("at Acme\u200b", ["Acme\u200b Care\u200b"])).toBeNull();
  });

  it("never echoes the term, ignores a term that is only invisible characters, and keeps whole-word matching", () => {
    const reason = privacyProblem("at Acme  Care", ["Acme Care"]);
    expect(reason).toBe(HELD);
    expect(reason).not.toMatch(/Acme/);
    expect(privacyProblem("nothing here", ["\u200b", " "])).toBeNull();
    expect(privacyProblem("the Acmeville\ndepot", ["Acme"])).toBeNull();
  });

  it("holds only an absolute home directory with a user segment (I5)", () => {
    const PATHS = "contains an email address or a home directory path";
    const table: [string, string | null][] = [
      ["/Users/alice/x", PATHS], ["see /home/bob/.ssh", PATHS], ["(/Users/alice/x)", PATHS], ["\"/home/x/y\"", PATHS],
      ["see C:\\Users\\joi\\x.ts", PATHS], ["see c:\\\\users\\\\joi", PATHS],
      ["~/.claude/settings.json", null], ["~/.agentic-workflow/scorer", null], ["src/api/users/x.ts", null], ["app/home/page.tsx", null],
      ["/users/me", null], ["github.com/users/foo", null], ["/users/:id", null], ["GET /users/{id}", null], ["a ~ b and src/x~/y", null],
    ];
    for (const [text, want] of table) expect(privacyProblem(text, []), text).toBe(want);
  });

  it("strips tag characters, variation selectors and U+061C, and holds a term with one inside (I4)", () => {
    expect(privacyProblem("at Ac\u{E0041}me today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Acme today", ["Ac\u{E0041}me"])).toBe(HELD);
    expect(privacyProblem("at Ac\u{E0100}me\u061c today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Ac\ufe0fme", ["Acme"])).toBe(HELD);
  });

  it("matches across combining marks and Turkish dotted and dotless i (m3)", () => {
    expect(privacyProblem("a Cafe\u0301 here", ["Caf\u00e9"])).toBe(HELD);
    expect(privacyProblem("a Caf\u00e9 here", ["Cafe"])).toBe(HELD);
    expect(privacyProblem("at A\u0332cme", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Acme", ["A\u0332cme"])).toBe(HELD);
    expect(privacyProblem("at A\u0130cmE", ["Aicme"])).toBe(HELD);
    expect(privacyProblem("at A\u0131cme", ["Aicme"])).toBe(HELD);
    expect(privacyProblem("at Acme", ["A\u0130cme"])).toBeNull();
  });
});

