import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { renderKeyRows, renderProfileDoc, renderSchemas } from "../src/docs/profile-doc.js";

const ROOT = path.resolve(import.meta.dirname, "../..");

describe("generated profile docs (run: cd sindri && npm run gen)", () => {
  it("schema files are up to date", () => {
    const { profile, repo } = renderSchemas();
    expect(fs.readFileSync(path.join(ROOT, "sindri/schema/profile.schema.json"), "utf8")).toBe(profile);
    expect(fs.readFileSync(path.join(ROOT, "sindri/schema/repo.schema.json"), "utf8")).toBe(repo);
  });

  it("docs/sindri/profile.md is up to date", () => {
    expect(fs.readFileSync(path.join(ROOT, "docs/sindri/profile.md"), "utf8")).toBe(renderProfileDoc());
  });

  it("renders enums, consts, unions, arrays, defaults and descriptions", () => {
    const rows = renderKeyRows({
      type: "object",
      required: ["a"],
      properties: {
        a: { enum: ["x", "y"], default: "x", description: "pick one" },
        b: { const: 1 },
        c: { anyOf: [{ type: "object" }, { type: "object" }] },
        d: { type: "array", items: { type: "string" } },
        e: {},
        f: { type: "array" },
      },
    });
    expect(rows).toEqual([
      "| `a` | `x` \\| `y` | yes | `\"x\"` | pick one |",
      "| `b` | `1` | no |  |  |",
      "| `c` | object (one of 2 shapes) | no |  |  |",
      "| `d` | string[] | no |  |  |",
      "| `e` | any | no |  |  |",
      "| `f` | any[] | no |  |  |",
    ]);
    expect(renderKeyRows({})).toEqual([]);
  });
});
