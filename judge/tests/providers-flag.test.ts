import { describe, expect, it } from "vitest";

import { parseProvidersAllowlist } from "../src/providers-flag.js";

describe("parseProvidersAllowlist", () => {
  it("returns null allowlist and untouched argv when neither flag nor env is set", () => {
    expect(parseProvidersAllowlist(["node", "judge", "why", "d1"], {})).toEqual({ ok: true, allowed: null, argv: ["node", "judge", "why", "d1"] });
  });

  it("parses --providers and strips it from argv wherever it appears", () => {
    expect(parseProvidersAllowlist(["node", "judge", "--providers", "jev,claude-cli", "ask-check"], {})).toEqual({
      ok: true,
      allowed: ["jev", "claude-cli"],
      argv: ["node", "judge", "ask-check"],
    });
  });

  it("falls back to AW_JUDGE_PROVIDERS when the flag is absent", () => {
    expect(parseProvidersAllowlist(["node", "judge", "health"], { AW_JUDGE_PROVIDERS: "jev" })).toEqual({ ok: true, allowed: ["jev"], argv: ["node", "judge", "health"] });
  });

  it("rejects unknown provider names instead of falling back to the full chain", () => {
    const r = parseProvidersAllowlist(["node", "judge", "--providers", "jev,gpt5"], {});
    expect(r.ok).toBe(false);
  });

  it("rejects an empty list and a missing value", () => {
    expect(parseProvidersAllowlist(["node", "judge", "--providers", ""], {}).ok).toBe(false);
    expect(parseProvidersAllowlist(["node", "judge", "--providers"], {}).ok).toBe(false);
  });
});
