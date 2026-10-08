import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { renderErrorsDoc } from "../src/docs/errors-doc.js";
import { cell } from "../src/docs/markdown.js";
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

  it("escapes <…> and | in table cells so GitHub renders them (M2)", () => {
    expect(cell("sindri/<user>/<item> a|b and `x <y> | z`")).toBe("sindri/&lt;user&gt;/&lt;item&gt; a\\|b and `x <y> \\| z`");
    expect(renderKeyRows({ properties: { k: { type: "string", default: "a|b", description: "repos/<name>.yaml" } } })).toEqual([
      "| `k` | string | no | `\"a\\|b\"` | repos/&lt;name&gt;.yaml |",
    ]);
    const doc = renderProfileDoc();
    expect(doc).toContain("## `repos/<name>.yaml`");
    expect(doc).toContain("sindri/&lt;user&gt;/&lt;item&gt;");
    expect(doc).not.toContain("sindri/<user>");
    expect(renderErrorsDoc()).toContain("--profile &lt;dir&gt;");
    expect(renderErrorsDoc()).toContain("`sindri profile approve <hash>`");
  });
});
