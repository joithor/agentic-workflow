import { describe, expect, it } from "vitest";

import { compiledGlob, globToRegExp, matchesAny } from "../src/index/globs.js";

describe("globToRegExp", () => {
  it.each([
    ["**/*.pem", "a/b/key.pem", true],
    ["**/*.pem", "key.pem", true],
    ["**/*.pem", "key.pem.txt", false],
    [".env*", ".env.local", true],
    [".env*", "app/.env", false],
    ["**/.env*", "app/.env", true],
    ["src/*.ts", "src/a.ts", true],
    ["src/*.ts", "src/x/a.ts", false],
    ["src/**", "src/x/y/z.ts", true],
    ["**/secrets/**", "a/secrets/b/c.ts", true],
    ["file?.ts", "file1.ts", true],
    ["a+b.(c).ts", "a+b.(c).ts", true],
    [".env*", ".ENV.local", true],
    ["**/*.pem", "keys/KEY.PEM", true],
    ["**/secrets/**", "a/secrets/x\ny.ts", true],
    ["**/*.pem", "a\nb/key.pem", true],
  ])("%s vs %s → %s", (glob, p, want) => {
    expect(globToRegExp(glob).test(p)).toBe(want);
  });

  it("matchesAny checks every glob", () => {
    expect(matchesAny("x/key.pem", ["src/**", "**/*.pem"])).toBe(true);
    expect(matchesAny("x/key.ts", [])).toBe(false);
  });
});

describe("compiledGlob", () => {
  it("compiles each glob once and reuses it", () => {
    expect(compiledGlob("src/**/*.ts")).toBe(compiledGlob("src/**/*.ts"));
    expect(compiledGlob("src/**/*.ts").test("src/a/b.ts")).toBe(true);
    expect(compiledGlob("src/**/*.ts")).not.toBe(globToRegExp("src/**/*.ts"));
  });
});
