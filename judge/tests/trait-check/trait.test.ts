import { describe, expect, it } from "vitest";

import { parseTrait } from "../../src/trait-check/trait.js";

const doc = (frontmatter: string, body = "") => `---\n${frontmatter}\n---\n${body}`;
const base = "name: static-imports-first\nsubject: Static imports.\nrequirement: >\n  Put static imports at the top.\npurpose: Find dependencies first.";

describe("parseTrait", () => {
  it("reads frontmatter fields, defaults severity to block and keeps the body as context", () => {
    const out = parseTrait(doc(`${base}\npaths: ["**/*.ts"]`, "\n```diff\n-a\n+b\n```\n"), "/r/.agents/traits/static-imports-first.md");
    expect(out).toEqual({
      ok: true,
      trait: {
        name: "static-imports-first",
        subject: "Static imports.",
        requirement: "Put static imports at the top.\n",
        purpose: "Find dependencies first.",
        paths: ["**/*.ts"],
        severity: "block",
        context: "```diff\n-a\n+b\n```",
        file: "/r/.agents/traits/static-imports-first.md",
      },
    });
  });

  it("omits context when the body is empty or absent, and accepts optional fields", () => {
    const withOptional = `${base}\napplicability: Only TS.\nguidance: Move them.\nseverity: warn`;
    expect(parseTrait(`---\n${withOptional}\n---`, "static-imports-first.md")).toMatchObject({
      ok: true,
      trait: { applicability: "Only TS.", guidance: "Move them.", severity: "warn" },
    });
    const out = parseTrait(doc(base, "   \n"), "static-imports-first.md");
    expect(out.ok && "context" in out.trait).toBe(false);
  });

  it("rejects a document without frontmatter", () => {
    expect(parseTrait("# just markdown", "x.md")).toEqual({ ok: false, file: "x.md", error: "missing or unterminated --- frontmatter" });
  });

  it("rejects invalid YAML with its first diagnostic line", () => {
    const out = parseTrait(doc("name: [unclosed"), "x.md");
    expect(out.ok).toBe(false);
    expect(!out.ok && out.error.startsWith("invalid YAML: ")).toBe(true);
  });

  it("rejects unknown keys and bad values, naming the field", () => {
    expect(parseTrait(doc(`${base}\nwhen: always`), "static-imports-first.md")).toMatchObject({ ok: false, error: expect.stringContaining("Unrecognized key") });
    expect(parseTrait(doc(base.replace("static-imports-first", "trait-bad")), "trait-bad.md")).toMatchObject({ ok: false, error: expect.stringMatching(/^name: /) });
    expect(parseTrait(doc("just a string"), "x.md")).toMatchObject({ ok: false, error: expect.stringMatching(/^frontmatter: /) });
    expect(parseTrait(doc(`${base}\npaths: ["src/{a,b.ts"]`), "static-imports-first.md")).toEqual({
      ok: false,
      file: "static-imports-first.md",
      error: "paths.0: unclosed { in glob",
    });
  });

  it("requires the name to match the file name", () => {
    expect(parseTrait(doc(base), "/r/other.md")).toEqual({ ok: false, file: "/r/other.md", error: 'name "static-imports-first" must match the file name "other.md"' });
  });
});
