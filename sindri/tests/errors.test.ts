import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { ERRORS, SindriError } from "../src/errors.js";
import { renderErrorsDoc } from "../src/docs/errors-doc.js";

const SRC = path.resolve(import.meta.dirname, "../src");
const CODE_RE = /SND-[A-Z]+-\d{3}/g;

function sourceFiles(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) return sourceFiles(p);
    return p.endsWith(".ts") ? [p] : [];
  });
}

describe("error registry", () => {
  it("every code matches SND-<AREA>-<NNN> and has a summary and a fix", () => {
    for (const [code, def] of Object.entries(ERRORS)) {
      expect(code).toMatch(/^SND-[A-Z]+-\d{3}$/);
      expect(def.summary.length).toBeGreaterThan(0);
      expect(def.fix.length).toBeGreaterThan(0);
    }
  });

  it("every code used in src is registered, and every registered code is used", () => {
    const used = new Set<string>();
    for (const file of sourceFiles(SRC)) {
      if (file.endsWith(path.join("src", "errors.ts"))) continue;
      for (const m of fs.readFileSync(file, "utf8").matchAll(CODE_RE)) used.add(m[0]);
    }
    const registered = new Set(Object.keys(ERRORS));
    expect([...used].filter((c) => !registered.has(c))).toEqual([]);
    expect([...registered].filter((c) => !used.has(c))).toEqual([]);
  });

  it("docs/sindri/errors.md is up to date (run: cd sindri && npm run gen)", () => {
    const doc = fs.readFileSync(path.resolve(import.meta.dirname, "../../docs/sindri/errors.md"), "utf8");
    expect(doc).toBe(renderErrorsDoc());
  });

  it("SindriError carries its code", () => {
    const e = new SindriError("SND-CLI-001", "nope");
    expect(e.code).toBe("SND-CLI-001");
    expect(e.message).toBe("nope");
    expect(e.name).toBe("SindriError");
  });
});
