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

  it("holds lowercase /users/<name>, C:\\Users\\<name> and ~/ paths", () => {
    const PATHS = "contains an email address or a home directory path";
    expect(privacyProblem("see /users/joi/x.ts", [])).toBe(PATHS);
    expect(privacyProblem("see /HOME/builder/x.ts", [])).toBe(PATHS);
    expect(privacyProblem("see C:\\Users\\joi\\x.ts", [])).toBe(PATHS);
    expect(privacyProblem("see c:\\\\users\\\\joi", [])).toBe(PATHS);
    expect(privacyProblem("see ~/notes/x.md", [])).toBe(PATHS);
    expect(privacyProblem("run (~/bin/tool)", [])).toBe(PATHS);
    expect(privacyProblem("a ~ b and src/x~/y", [])).toBeNull();
  });
});

