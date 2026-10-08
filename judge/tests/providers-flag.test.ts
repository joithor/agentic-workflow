import { describe, expect, it } from "vitest";

import { adjudicateRefusal, gatePromptSortDeps, isProviderAllowed, parseProvidersAllowlist } from "../src/providers-flag.js";

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

describe("provider allowlist gating outside the evaluate chain", () => {
  it("allows every provider when no allowlist is set", () => {
    expect(isProviderAllowed(null, "jev")).toBe(true);
    expect(isProviderAllowed(null, "claude-cli")).toBe(true);
  });

  it("allows only listed providers when an allowlist is set", () => {
    expect(isProviderAllowed(["claude-cli"], "claude-cli")).toBe(true);
    expect(isProviderAllowed(["claude-cli"], "jev")).toBe(false);
  });

  it("prompt-sort keeps jev and the adjudicator when no allowlist is set", () => {
    const jev = { id: "jev" };
    const adj = (): string => "adj";
    expect(gatePromptSortDeps(null, jev, adj)).toEqual({ jev, adjudicator: adj });
  });

  it("prompt-sort drops jev (falls back to its rules path) when jev is not allowed", () => {
    const jev = { id: "jev" };
    const adj = (): string => "adj";
    expect(gatePromptSortDeps(["claude-cli"], jev, adj)).toEqual({ jev: null, adjudicator: adj });
  });

  it("prompt-sort drops its adjudicator when claude-cli is not allowed", () => {
    const jev = { id: "jev" };
    const adj = (): string => "adj";
    expect(gatePromptSortDeps(["jev"], jev, adj)).toEqual({ jev, adjudicator: null });
  });

  it("prompt-sort passes absent deps through as null", () => {
    expect(gatePromptSortDeps(["jev"], null, null)).toEqual({ jev: null, adjudicator: null });
  });

  it("adjudicate refuses with usage exit 64 when claude-cli is not allowed", () => {
    const r = adjudicateRefusal(["jev"]);
    expect(r).toMatchObject({ exitCode: 64, stdout: "" });
    expect(r?.stderr).toContain("claude-cli");
  });

  it("adjudicate is not refused when claude-cli is allowed or no allowlist is set", () => {
    expect(adjudicateRefusal(null)).toBeNull();
    expect(adjudicateRefusal(["claude-cli"])).toBeNull();
  });
});
