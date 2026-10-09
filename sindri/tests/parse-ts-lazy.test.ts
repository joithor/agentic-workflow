import { createRequire } from "node:module";
import path from "node:path";
import { describe, expect, it } from "vitest";

const loaded = (): boolean =>
  Object.keys(createRequire(import.meta.url).cache).some((k) => k.split(path.sep).join("/").endsWith("/typescript/lib/typescript.js"));

describe("typescript is loaded lazily", () => {
  it("not at import, and only on the first parse", async () => {
    const { typescriptParser } = await import("../src/index/parse-ts.js");
    expect(loaded()).toBe(false);
    expect(typescriptParser.supports("a.ts")).toBe(true);
    expect(loaded()).toBe(false);
    typescriptParser.parse("a.ts", "export const a = () => 1;");
    expect(loaded()).toBe(true);
  });
});
