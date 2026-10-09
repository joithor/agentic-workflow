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
