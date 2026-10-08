import { describe, expect, it } from "vitest";

import { SindriError } from "../src/errors.js";
import { compileExtraPatterns, makeScrubber, regexRisk } from "../src/scrub/scrub.js";

const s = makeScrubber();
const ASSIGNED = "g".repeat(12) + "1" + "g".repeat(11);

// [input, kind, the part that must disappear]
const CASES: [string, string, string][] = [
  ["key: -----BEGIN " + "RSA PRIVATE KEY-----\nMIIabc\n-----END RSA PRIVATE KEY-----", "private-key", "MIIabc"],
  ["token " + "sk-" + "ant-oat01-" + "a".repeat(30), "anthropic-key", "a".repeat(30)],
  ["key " + "sk-" + "proj-" + "b".repeat(30), "openai-key", "b".repeat(30)],
  ["aws " + "AKIA" + "ABCDEFGHIJKLMNOP", "aws-access-key", "ABCDEFGHIJKLMNOP"],
  ["gh " + "ghp" + "_" + "c".repeat(36), "github-token", "c".repeat(36)],
  ["pat " + "github" + "_pat_" + "d".repeat(30), "github-token", "d".repeat(30)],
  ["slack " + "xox" + "b-" + "1234567890-abcdef", "slack-token", "1234567890-abcdef"],
  ["stripe " + "sk" + "_live_" + "s".repeat(24), "stripe-key", "s".repeat(24)],
  ["google " + "AI" + "za" + "t".repeat(35), "google-api-key", "t".repeat(35)],
  ["linear " + "lin" + "_api_" + "e".repeat(40), "linear-key", "e".repeat(40)],
  ["jwt " + "eyJ" + "hbGciOiJIUzI1" + "." + "eyJzdWIiOiIx" + "." + "SflKxwRJSMeKKF2", "jwt", "SflKxwRJSMeKKF2"],
  ["Authorization: " + "Bearer " + "f".repeat(30), "bearer", "f".repeat(30)],
  ["https://" + "user:" + "hunter2pass" + "@example.com/x", "credentialed-url", "hunter2pass"],
  ["GITHUB_" + "TOKEN=" + ASSIGNED, "secret-assignment", ASSIGNED],
  ["ssn " + "123" + "-45-" + "6789", "ssn", "6789"],
  ["MRN" + ": 12345678", "mrn", "12345678"],
];

describe("scrubber", () => {
  it.each(CASES)("redacts %#", (input, kind, secret) => {
    const hits = s.find(input);
    expect(hits.map((h) => h.kind)).toEqual([kind]);
    const out = s.scrub(input);
    expect(out.text).not.toContain(secret);
    expect(out.text).toContain(`[REDACTED:${kind}]`);
    expect(JSON.stringify(out.hits)).not.toContain(secret);
  });

  it("keeps the variable name and URL host readable", () => {
    expect(s.scrub("GITHUB_" + "TOKEN=" + ASSIGNED).text).toBe("GITHUB_TOKEN=[REDACTED:secret-assignment]");
    expect(s.scrub("https://" + "user:" + "hunter2pass" + "@example.com/x").text).toBe(
      "https://user:[REDACTED:credentialed-url]@example.com/x",
    );
  });

  it("is idempotent: its own placeholder is never a hit (M9)", () => {
    const inputs = [...CASES.map(([input]) => input), CASES.map(([input]) => input).join("\n"), "https://" + "user:" + "[REDACTED:credentialed-url]" + "@example.com/x"];
    for (const input of inputs) {
      const once = s.scrub(input).text;
      expect(s.find(once)).toEqual([]);
      expect(s.scrub(once).text).toBe(once);
    }
    // A real secret beside a placeholder, before or after it, is still found.
    expect(s.find("[REDACTED:jwt] " + "AKIA" + "ABCDEFGHIJKLMNOP").map((h) => h.kind)).toEqual(["aws-access-key"]);
    expect(s.find("AKIA" + "ABCDEFGHIJKLMNOP" + " [REDACTED:jwt]").map((h) => h.kind)).toEqual(["aws-access-key"]);
  });

  it("stays linear on an unterminated private-key header", () => {
    const text = ("-----BEGIN " + "PRIVATE KEY-----\n").repeat(2000);
    expect(s.find(text)).toEqual([]);
  });

  it("reports one hit for overlapping matches, using the first pattern's kind", () => {
    const hits = s.find("sk-" + "ant-api03-" + "h".repeat(40));
    expect(hits).toHaveLength(1);
    expect(hits[0].kind).toBe("anthropic-key");
  });

  it("leaves ordinary engineering text alone (Review Focus 5)", () => {
    const text = [
      "commit 3f786850e387550fdab836ed7e6dc881de23001b",
      "uuid 123e4567-e89b-12d3-a456-426614174000",
      "ulid 01k6zq7v8m3n4p5q6r7s8t9v0w",
      "date 2026-10-08T12:00:00Z",
      "phone 555-123-4567",
      "version 1.2.3-beta.4",
      "use sk-learn for this",
      "the risk-assessment-for-the-quarterly-plan doc",
      "token: short",
      "token: SecretPointerSchema,",
      "apiKey: config.providers.anthropic.key",
      "refreshToken: RefreshTokenSchemaV2,",
      "const token = generateToken256Bits();",
      "apiKey: makeKey(config2),",
    ].join("\n");
    expect(s.find(text)).toEqual([]);
    expect(s.scrub(text).text).toBe(text);
  });

  it("scrubs strings and keys deep inside objects and arrays", () => {
    const secret = "AKIA" + "ABCDEFGHIJKLMNOP";
    const out = s.scrubDeep({ a: [secret, 1, null], [secret]: { b: secret }, n: 2 });
    expect(JSON.stringify(out)).not.toContain(secret);
    expect(out).toEqual({
      a: ["[REDACTED:aws-access-key]", 1, null],
      "[REDACTED:aws-access-key]": { b: "[REDACTED:aws-access-key]" },
      n: 2,
    });
  });

  it("adds profile patterns (add-only) and rejects ones that don't compile", () => {
    const extra = compileExtraPatterns([{ kind: "employee-id", regex: "\\bEMP-\\d{6}\\b" }]);
    const withExtra = makeScrubber(extra);
    expect(withExtra.scrub("id EMP-123456").text).toBe("id [REDACTED:employee-id]");
    expect(withExtra.find("AKIA" + "ABCDEFGHIJKLMNOP")[0].kind).toBe("aws-access-key");
    expect(() => compileExtraPatterns([{ kind: "ok", regex: "a" }, { kind: "bad", regex: "(" }])).toThrow(SindriError);
    expect(() => compileExtraPatterns([{ kind: "redos", regex: "(a+)+$" }])).toThrow(/nested quantifier/);
    expect(() => compileExtraPatterns([{ kind: "redos", regex: "(\\w*){2,}" }])).toThrow(/nested quantifier/);
    expect(() => compileExtraPatterns([{ kind: "alt", regex: "(a|aa)+$" }])).toThrow(/quantified alternation/);
    expect(() => compileExtraPatterns([{ kind: "redos", regex: "((a+))+$" }])).toThrow(/nested quantifier/);
    expect(() => compileExtraPatterns([{ kind: "redos", regex: "\\w+\\w+\\w+!" }])).toThrow(/two unbounded quantifiers in a row/);
    expect(() => compileExtraPatterns([{ kind: "redos", regex: "[a-z]+[a-z]+[a-z]+!" }])).toThrow(/two unbounded quantifiers in a row/);
    expect(() => compileExtraPatterns([{ kind: "empty", regex: "x*" }])).toThrow(/empty string/);
    expect(compileExtraPatterns([{ kind: "fine", regex: "(?:ab|cd)x" }])).toHaveLength(1);
    try {
      compileExtraPatterns([{ kind: "bad", regex: "(" }]);
    } catch (e) {
      expect((e as SindriError).code).toBe("SND-SCRUB-001");
      expect((e as SindriError).message).toContain("scrub.extraPatterns[0]");
    }
  });
});

describe("scrubber: PR #69 review", () => {
  const V = ASSIGNED;
  // AWS secret keys often have "/" before their first digit.
  const AWS_SECRET = "wJalrXUtnFEMI/K" + "7MDENG/bPxRfiCY" + "EXAMPLEKEY";

  it.each([
    [`"api_` + `key": "${V}"`, V],
    [`{"client` + `Secret":"${V}"}`, V],
    // Quoted since PR #69 run 2: an unquoted code-style value no longer counts.
    [`access` + `Token="${V}"`, V],
    [`client` + `Secret = "${V}"`, V],
    [`db` + `Password='${V}'`, V], // quoted (run 2 ruling)
    [`X-API-` + `Key: ${V}`, V],
    [`AWS_SECRET_ACCESS_` + `KEY=${AWS_SECRET}`, AWS_SECRET],
    [`aws_secret_access_` + `key = "${AWS_SECRET}"`, AWS_SECRET], // quoted (run 2 ruling)
    [`export X_` + `TOKEN=${V}`, V],
    [`x-auth-` + `token: ${V}`, V],
  ])("redacts the secret-assignment shape %#", (input, secret) => {
    expect(s.find(input).map((h) => h.kind)).toEqual(["secret-assignment"]);
    expect(s.scrub(input).text).not.toContain(secret);
  });

  it("keeps identifiers that merely end in a secret word", () => {
    const text = ["access" + "Token=" + V, "tokenizer: " + V, "password_hash: " + V, "const apiKey = process.env.API_KEY", "token = response.data.token"].join("\n");
    expect(s.find(text)).toEqual([]);
  });

  it("stays linear on a long run with no digit", () => {
    const start = Date.now();
    s.find("token=".repeat(100_000));
    expect(Date.now() - start).toBeLessThan(2000);
  });

  it("scans a crafted placeholder that holds a secret (R1), and stays idempotent", () => {
    const aws = "AKIA" + "ABCDEFGHIJKLMNOP";
    const gh = "ghp" + "_" + "c".repeat(36);
    expect(s.find(`[REDACTED:${aws}]`).map((h) => h.kind)).toEqual(["aws-access-key"]);
    expect(s.find(`[REDACTED:${gh}]`).map((h) => h.kind)).toEqual(["github-token"]);
    const once = s.scrub(`[REDACTED:${aws}] and [REDACTED:${gh}]`).text;
    expect(once).toBe("[REDACTED:[REDACTED:aws-access-key]] and [REDACTED:[REDACTED:github-token]]");
    expect(s.scrub(once).text).toBe(once);
    // A profile kind's placeholder is skipped too.
    const extra = makeScrubber(compileExtraPatterns([{ kind: "emp-id", regex: "EMP-\\d{6}" }]));
    expect(extra.scrub(extra.scrub("EMP-123456").text).text).toBe("[REDACTED:emp-id]");
  });

  it.each([
    ["(a+)+", "a nested quantifier"],
    ["((a+))+", "a nested quantifier"],
    ["(?:(?:a*)){2,3}", "a nested quantifier"],
    ["(?<n>a?)+", "a nested quantifier"],
    ["(a|b)*", "a quantified alternation"],
    ["((a|b))+", "a quantified alternation"],
    ["\\w+\\w*", "two unbounded quantifiers in a row"],
    ["[a-z]+?[^\\]x]{2,}", "two unbounded quantifiers in a row"],
  ])("regexRisk refuses %s", (re, why) => {
    expect(regexRisk(re)).toBe(why);
  });

  it.each([
    "\\bEMP-\\d{6}\\b", "\\bMRN[\\s:#-]*\\d{6,10}\\b", "(?:ab|cd)x", "(a|b)?c", "(?:\\d{3})?-\\d+", "(?=x)a+", "(?<=x)a+", "(?<!x)(?!y)a",
    "a+ b+", "x{0,1}y", "[]a]+", "a{2}", "x{,3}", "a)b", "(a",
  ])("regexRisk allows %s", (re) => {
    expect(regexRisk(re)).toBeNull();
  });
});
