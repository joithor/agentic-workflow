import { describe, expect, it } from "vitest";

import { globToRegExp, hasUnclosedBrace, inScope } from "../../src/trait-check/glob.js";

const matches = (glob: string, file: string) => globToRegExp(glob).test(file);

describe("globToRegExp", () => {
  it("lets ** span directories, including none", () => {
    expect(matches("**/*.ts", "a.ts")).toBe(true);
    expect(matches("**/*.ts", "a/b/c.ts")).toBe(true);
    expect(matches("packages/**", "packages/x/y.ts")).toBe(true);
    expect(matches("packages/**", "apps/x.ts")).toBe(false);
  });

  it("keeps * and ? inside one segment", () => {
    expect(matches("src/*.ts", "src/a.ts")).toBe(true);
    expect(matches("src/*.ts", "src/a/b.ts")).toBe(false);
    expect(matches("src/?.ts", "src/a.ts")).toBe(true);
    expect(matches("src/?.ts", "src/ab.ts")).toBe(false);
  });

  it("supports {a,b} alternation and escapes regex characters", () => {
    expect(matches("**/*.{ts,tsx}", "x/y.tsx")).toBe(true);
    expect(matches("**/*.{ts,tsx}", "x/y.js")).toBe(false);
    expect(matches("a+b.(x).ts", "a+b.(x).ts")).toBe(true);
    expect(matches("a.ts", "abts")).toBe(false);
    expect(matches("a,b}.ts", "a,b}.ts")).toBe(true);
  });

  it("treats an unclosed { as a literal instead of throwing", () => {
    expect(matches("src/{a,b.ts", "src/{a,b.ts")).toBe(true);
    expect(matches("src/{a,b.ts", "src/a.ts")).toBe(false);
    expect(matches("{x,{a,b}}.ts", "a.ts")).toBe(true);
  });
});

describe("hasUnclosedBrace", () => {
  it("flags only a { without a matching }", () => {
    expect(hasUnclosedBrace("src/{a,b.ts")).toBe(true);
    expect(hasUnclosedBrace("src/{a,b}.ts")).toBe(false);
    expect(hasUnclosedBrace("src/a}.ts")).toBe(false);
  });
});

describe("inScope", () => {
  it("treats a trait without paths as applying everywhere", () => {
    expect(inScope(undefined, "any/file.md")).toBe(true);
  });

  it("matches when any glob matches", () => {
    expect(inScope(["apps/**", "packages/**/*.ts"], "packages/a/b.ts")).toBe(true);
    expect(inScope(["apps/**"], "packages/a/b.ts")).toBe(false);
  });
});
