import { describe, expect, it } from "vitest";

import { typescriptParser } from "../src/index/parse-ts.js";

const SRC = `
import { helper } from "./helper";

export function add(a: number, b: number): number {
  return a + b;
}

export function sum(x: number, y: number): number {
  return x + y;
}

export const mul = (a: number, b = 2) => helper(a * b);

const fn = function (s: string) { return s.trim(); };

export default function () { return 1; }

export class Store {
  save(id: string): void {
    if (id && this.ok) { log.info(id); } else { log.warn(id); }
    for (const x of [1, 2]) { while (x > 3) { break; } }
  }
  private hidden(): boolean { return true ? false : true; }
}

class Internal {
  run() { const inner = () => 0; return inner(); }
}
`;

describe("typescriptParser", () => {
  const syms = typescriptParser.parse("src/math.ts", SRC);
  const by = (name: string) => {
    const s = syms.find((x) => x.name === name);
    if (s === undefined) throw new Error(`no symbol ${name}`);
    return s;
  };

  it("finds functions, arrows, function expressions, default exports, classes and methods", () => {
    expect(syms.map((s) => [s.name, s.kind, s.exported])).toEqual([
      ["add", "function", true],
      ["sum", "function", true],
      ["mul", "arrow", true],
      ["fn", "arrow", false],
      ["default", "function", true],
      ["Store", "class", true],
      ["Store.save", "method", true],
      ["Store.hidden", "method", false],
      ["Internal", "class", false],
      ["Internal.run", "method", false],
      ["inner", "arrow", false],
    ]);
  });

  it("records lines, signatures and capped text", () => {
    expect(by("add")).toMatchObject({ file: "src/math.ts", startLine: 4, endLine: 6, signature: "(a: number, b: number): number" });
    expect(by("mul").signature).toBe("(a: number, b = 2)");
    expect(by("Store").signature).toBe("");
    expect(by("add").text.startsWith("export function add")).toBe(true);
  });

  it("gives renamed clones the same hash and different structure a different one", () => {
    expect(by("add").astHash).toBe(by("sum").astHash);
    expect(by("add").astHash).not.toBe(by("mul").astHash);
    expect(by("add").tokens).toContain("$id");
    expect(by("mul").tokens).toContain("$lit");
  });

  it("counts decision points and callees", () => {
    expect(by("add").complexity).toBe(1);
    expect(by("Store.save").complexity).toBe(5); // if, &&, for-of, while
    expect(by("Store.hidden").complexity).toBe(2); // ?:
    expect(by("Store.save").callees).toEqual(["info", "warn"]);
    expect(by("mul").callees).toEqual(["helper"]);
    expect(by("Internal.run").callees).toEqual(["inner"]);
  });

  it("parses JS, JSX and TSX, tolerates syntax errors, and caps text", () => {
    expect(typescriptParser.parse("a.js", "function f(){ return g(); }").map((s) => s.name)).toEqual(["f"]);
    expect(typescriptParser.parse("a.jsx", "const C = () => <div/>;").map((s) => s.name)).toEqual(["C"]);
    const anon = typescriptParser.parse("c.ts", "export default class { run() { return 1; } }");
    expect(anon.map((s) => [s.name, s.kind, s.exported])).toEqual([["default", "class", true], ["default.run", "method", true]]);
    expect(typescriptParser.parse("a.tsx", "export const C = (): JSX.Element => <div/>;").map((s) => s.kind)).toEqual(["arrow"]);
    expect(() => typescriptParser.parse("bad.ts", "function ( {")).not.toThrow();
    const long = typescriptParser.parse("l.ts", `function big() { return "${"x".repeat(5000)}"; }`);
    expect(long[0].text).toHaveLength(4000);
  });

  it("supports TS and JS paths only", () => {
    expect(typescriptParser.supports("a.mts")).toBe(true);
    expect(typescriptParser.supports("a.d.ts")).toBe(false);
    expect(typescriptParser.supports("a.py")).toBe(false);
  });

  it("skips bodiless declarations and covers remaining branches", () => {
    const src = `
declare function d(a: string): void;
function over(a: string): void;
function over(a: number): void;
function over(a: unknown): void { call(a); }
abstract class A { abstract m(): void; n(): void; }
const o = { m() { return 1; } };
const x = 5, y = (z, w) => z ?? w;
const { p } = { p: () => 1 };
class B { constructor() {} ["k"]() { a.b.c(); (0, f)(); } static s() { do { } while (0); try { } catch (e) { } switch (1) { case 1: break; default: } } }
const t = \`a\${1}\`; const u = true; const v = false; const w2 = #priv in o;
class C { #q() { return this.#q(); } }
`;
    const out = typescriptParser.parse("b.ts", src);
    expect(out.map((s) => s.name)).toEqual(["over", "A", "y", "B", "B.[\"k\"]", "B.s", "C", "C.#q"]);
  });
});
