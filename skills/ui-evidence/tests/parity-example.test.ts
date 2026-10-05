// The checked-in worked example must stay valid against the real schemas.
import fs from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { guardWrites } from "../src/host-guard.js";
import { viewportShortfall } from "../src/parity.js";
import { buildAttachmentPlan, renderLinearComment, type ParitySummary } from "../src/parity-publish.js";
import { parseDesignManifest } from "../src/parity-schema.js";

const dir = path.join(import.meta.dirname, "..", "examples", "design-parity");
const read = (f: string): unknown => JSON.parse(fs.readFileSync(path.join(dir, f), "utf8"));

describe("design-parity worked example", () => {
  it("manifest parses, is write-free and every frame fits its viewport", () => {
    const m = parseDesignManifest(read("example-manifest.json"));
    if ("error" in m) throw new Error(m.error);
    expect(guardWrites(m.frames.flatMap((f) => f.steps ?? []), false)).toEqual({ ok: true });
    for (const f of m.frames) expect(f.designRegion.y + f.designRegion.h).toBeLessThanOrEqual(m.viewport.h);
  });
  it("parity.json frames fit the recorded viewport (the driver would have thrown otherwise)", () => {
    const s = read("example-parity.json") as ParitySummary;
    for (const e of Object.values(s.frames)) expect(viewportShortfall({ ...e.implementationOrigin, w: e.designRegion.w, h: e.designRegion.h }, s.viewport)).toBeNull();
  });
  it("parity.json plans one titled Linear attachment per state", () => {
    const s = read("example-parity.json") as ParitySummary;
    const plan = buildAttachmentPlan(s, "/runs/frn-4241-example", () => 1);
    expect(plan.map((p) => p.title)).toEqual(["A-empty", "A2-column", "B-filled-expanded", "C-popover"].map((n) => `Pixel diff: ${n} (Figma | implementation | diff)`));
    expect(plan[0]?.subtitle).toContain("diff 0.91% (2295/251672 px)");
    expect(renderLinearComment(s)).toContain("| C-popover | 1.01% (1258/124800) |");
  });
  it("ships the side-by-side image the docs point at", () => {
    expect(fs.statSync(path.join(dir, "example-side-by-side.png")).size).toBeGreaterThan(1000);
  });
});
