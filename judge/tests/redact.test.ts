import { describe, expect, it } from "vitest";

import { capJson, capJsonValue, capText, redactDeep, redactSecrets } from "../src/redact.js";

describe("redactSecrets (RF-1)", () => {
  it.each([
    ["sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345"],
    ["ghp_abcdefghijklmnopqrstuvwxyz0123456789"],
    ["gho_abcdefghijklmnopqrstuvwxyz0123456789"],
    ["AKIAABCDEFGHIJKLMNOP"],
    ["xoxb-1234567890-abcdefghij"],
    ["eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.abcdefghijklmnop"],
    ["0123456789abcdef0123456789abcdef01234567"],
  ])("replaces %s with [REDACTED]", (secret) => {
    expect(redactSecrets(`key=${secret} rest`)).toBe("key=[REDACTED] rest");
  });

  it("leaves ordinary text and short hex (commit shas in prose) alone", () => {
    expect(redactSecrets("fix in 855d6f8, see chain.ts")).toBe("fix in 855d6f8, see chain.ts");
  });
});

describe("capJson", () => {
  it("serializes under the cap unchanged", () => {
    expect(capJson({ a: 1 }, 100)).toBe('{"a":1}');
  });
  it("truncates over the cap with a visible marker", () => {
    const out = capJson({ text: "x".repeat(50) }, 20);
    expect(out.startsWith('{"text":"xxxxxxxxxx')).toBe(true);
    expect(out).toMatch(/\[truncated \d+ chars\]$/);
  });
});

describe("redactDeep", () => {
  it("redacts strings, nested arrays/objects and keys, and passes primitives through", () => {
    const secret = "sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345";
    expect(redactDeep({ a: [`x ${secret}`, 1, null, true], [secret]: { b: secret } })).toEqual({
      a: ["x [REDACTED]", 1, null, true],
      "[REDACTED]": { b: "[REDACTED]" },
    });
  });
});

describe("capText", () => {
  it("returns text at or under the cap unchanged", () => {
    expect(capText("abc", 3)).toBe("abc");
  });
  it("truncates over the cap with a visible marker", () => {
    expect(capText("abcdef", 4)).toBe("abcd[truncated 2 chars]");
  });
});

describe("capJsonValue", () => {
  it("returns the plain serialization when it fits", () => {
    expect(capJsonValue({ a: 1 }, 100)).toBe('{"a":1}');
  });
  it("shortens the longest strings, nested in arrays and objects, and always parses", () => {
    const value = { a: "a".repeat(5000), list: ["b".repeat(3000), { c: "c".repeat(8000) }], n: 1, nil: null };
    const out = capJsonValue(value, 4000);
    expect(out.length).toBeLessThanOrEqual(4000);
    const parsed = JSON.parse(out) as typeof value & { list: [string, { c: string }] };
    expect(parsed.n).toBe(1);
    expect(parsed.list[1].c).toMatch(/\[truncated \d+ chars\]$/);
    expect(value.a).toHaveLength(5000);
  });
  it("falls back to a valid stub when there is nothing long to shorten", () => {
    const out = capJsonValue({ nums: Array.from({ length: 5000 }, (_, i) => i) }, 1000);
    expect(out.length).toBeLessThanOrEqual(1000);
    expect(JSON.parse(out)).toHaveProperty("truncated");
  });
});
