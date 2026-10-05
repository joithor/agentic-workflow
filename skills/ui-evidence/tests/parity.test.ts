import { PNG } from "pngjs";
import { describe, expect, it } from "vitest";

import { buildParityEntry, checkBoxes, compareRegion, cropRegion, failedBoxChecks, implementationRegion, selectorKey, sideBySide, viewportShortfall } from "../src/parity.js";

function solid(w: number, h: number, rgb: [number, number, number]): PNG {
  const p = new PNG({ width: w, height: h });
  for (let i = 0; i < w * h; i++) p.data.set([...rgb, 255], i * 4);
  return p;
}
const px = (p: PNG, x: number, y: number): number[] => [...p.data.subarray((y * p.width + x) * 4, (y * p.width + x) * 4 + 4)];

describe("cropRegion", () => {
  it("copies exactly the requested pixels", () => {
    const src = solid(6, 6, [0, 0, 0]);
    src.data.set([255, 0, 0, 255], (2 * 6 + 3) * 4);
    const out = cropRegion(src, { x: 3, y: 2, w: 2, h: 2 });
    expect([out.width, out.height]).toEqual([2, 2]);
    expect(px(out, 0, 0)).toEqual([255, 0, 0, 255]);
    expect(px(out, 1, 1)).toEqual([0, 0, 0, 255]);
  });

  it.each([
    [{ x: -1, y: 0, w: 2, h: 2 }],
    [{ x: 0, y: -1, w: 2, h: 2 }],
    [{ x: 5, y: 0, w: 2, h: 2 }],
    [{ x: 0, y: 5, w: 2, h: 2 }],
    [{ x: 0, y: 0, w: 0, h: 2 }],
    [{ x: 0.5, y: 0, w: 2, h: 2 }],
  ])("throws instead of silently clipping %j", (region) => {
    expect(() => cropRegion(solid(6, 6, [0, 0, 0]), region)).toThrow(RangeError);
  });
});

describe("implementationRegion", () => {
  it("anchors at the bbox origin, rounded, at the design size", () => {
    expect(implementationRegion({ x: 323.6, y: 257.2, w: 652, h: 386 }, { x: 328, y: 234, w: 652, h: 386 })).toEqual({ x: 324, y: 257, w: 652, h: 386 });
  });
  it("applies the anchor offset", () => {
    expect(implementationRegion({ x: 324, y: 257, w: 1, h: 1 }, { x: 0, y: 0, w: 10, h: 20 }, { x: 0, y: -82 })).toEqual({ x: 324, y: 175, w: 10, h: 20 });
  });
});

describe("viewportShortfall", () => {
  it("is null when the region fits and names the needed viewport when it does not", () => {
    expect(viewportShortfall({ x: 324, y: 175, w: 652, h: 830 }, { w: 1512, h: 1100 })).toBeNull();
    expect(viewportShortfall({ x: 324, y: 175, w: 652, h: 830 }, { w: 1512, h: 982 })).toEqual({ w: 1512, h: 1005 });
    expect(viewportShortfall({ x: 1000, y: 0, w: 600, h: 10 }, { w: 1512, h: 982 })).toEqual({ w: 1600, h: 982 });
  });
});

describe("compareRegion", () => {
  it("reports zero for identical images", () => {
    const r = compareRegion(solid(4, 4, [10, 20, 30]), solid(4, 4, [10, 20, 30]));
    expect(r).toMatchObject({ diffPixels: 0, total: 16, diffPercent: 0 });
  });
  it("counts differing pixels and a rounded percentage", () => {
    const b = solid(4, 4, [255, 255, 255]);
    b.data.set([0, 0, 0, 255], 0);
    const r = compareRegion(solid(4, 4, [255, 255, 255]), b);
    expect(r.diffPixels).toBe(1);
    expect(r.diffPercent).toBe(6.25);
    expect([r.diff.width, r.diff.height]).toEqual([4, 4]);
  });
  it("honours a loose threshold", () => {
    const a = solid(2, 2, [100, 100, 100]);
    const b = solid(2, 2, [110, 110, 110]);
    expect(compareRegion(a, b, { threshold: 0 }).diffPixels).toBe(4);
    expect(compareRegion(a, b, { threshold: 0.5, includeAA: true }).diffPixels).toBe(0);
  });
  it("throws on a size mismatch rather than comparing a partial area", () => {
    expect(() => compareRegion(solid(2, 2, [0, 0, 0]), solid(3, 2, [0, 0, 0]))).toThrow(/size mismatch/);
  });
});

describe("sideBySide", () => {
  it("lays out design | implementation | diff with white gaps", () => {
    const out = sideBySide(solid(2, 3, [255, 0, 0]), solid(2, 2, [0, 255, 0]), solid(1, 1, [0, 0, 255]), 4);
    expect([out.width, out.height]).toEqual([2 + 2 + 1 + 8, 3]);
    expect(px(out, 0, 0)).toEqual([255, 0, 0, 255]);
    expect(px(out, 2, 0)).toEqual([255, 255, 255, 255]);
    expect(px(out, 6, 0)).toEqual([0, 255, 0, 255]);
    expect(px(out, 6, 2)).toEqual([255, 255, 255, 255]);
    expect(px(out, 12, 0)).toEqual([0, 0, 255, 255]);
  });
  it("defaults to a 12px gap", () => {
    expect(sideBySide(solid(1, 1, [0, 0, 0]), solid(1, 1, [0, 0, 0]), solid(1, 1, [0, 0, 0])).width).toBe(3 + 24);
  });
});

describe("checkBoxes", () => {
  const boxes: Record<string, { x: number; y: number; w: number; h: number }> = {
    "testId:card": { x: 324, y: 257, w: 652, h: 386 },
    "testId:pop": { x: 598, y: 441, w: 400, h: 312 },
  };
  const measure = (s: Parameters<typeof selectorKey>[0]) => boxes[selectorKey(s)] ?? null;

  it("passes on exact size and an offset relative to another element", () => {
    const [c] = checkBoxes([{ target: { testId: "pop" }, relativeTo: { testId: "card" }, expected: { w: 400, h: 312, x: 274, y: 184 } }], measure);
    expect(c).toMatchObject({ pass: true, actual: { w: 400, h: 312, x: 274, y: 184 }, tolerance: 0, relativeTo: "testId:card" });
  });
  it("fails beyond tolerance and names every axis that missed", () => {
    const [c] = checkBoxes([{ target: { testId: "card" }, expected: { w: 650, h: 386 } }], measure);
    expect(c).toMatchObject({ pass: false, failure: "w 652 != 650" });
  });
  it("accepts a deviation within tolerance", () => {
    expect(checkBoxes([{ target: { testId: "card" }, expected: { w: 650 }, tolerance: 2 }], measure)[0]?.pass).toBe(true);
  });
  it("fails when the target or the reference element is missing", () => {
    const [a, b] = checkBoxes(
      [
        { target: { text: "nope" }, expected: { w: 1 } },
        { target: { testId: "card" }, relativeTo: { text: "gone" }, expected: { x: 0 } },
      ],
      measure,
    );
    expect(a).toMatchObject({ pass: false, actual: null, failure: "text:nope not found" });
    expect(b).toMatchObject({ pass: false, actual: null, failure: "text:gone not found" });
  });
});

describe("parity entries", () => {
  const files = { design: "d", implementation: "i", diff: "x", sideBySide: "s" };
  const entry = (pass: boolean) =>
    buildParityEntry({
      designRegion: { x: 328, y: 234, w: 652, h: 386 },
      anchorBox: { x: 324, y: 257, w: 652, h: 390 },
      anchorOffset: { x: 0, y: 0 },
      comparison: { diffPixels: 2295, total: 251672, diffPercent: 0.91 },
      threshold: 0.1,
      includeAA: false,
      boxChecks: [{ target: "testId:card", relativeTo: null, expected: { w: 652 }, actual: null, tolerance: 0, pass, ...(pass ? {} : { failure: "boom" }) }],
      knownDifferences: ["heading copy"],
      files,
    });

  it("records origin, size delta and the explained difference", () => {
    expect(entry(true)).toMatchObject({ implementationOrigin: { x: 324, y: 257 }, sizeDelta: { w: 0, h: 4 }, diffPercent: 0.91, knownDifferences: ["heading copy"], files });
  });
  it("rounds a fractional size delta and records the frame's design node", () => {
    const e = buildParityEntry({
      designRegion: { x: 0, y: 0, w: 652, h: 386 }, anchorBox: { x: 0, y: 0, w: 651.6, h: 386 }, anchorOffset: { x: 0, y: 0 },
      comparison: { diffPixels: 0, total: 1, diffPercent: 0 }, threshold: 0.1, includeAA: false, boxChecks: [], knownDifferences: [], files, designNode: "9:9",
    });
    expect(e.sizeDelta).toEqual({ w: -0.4, h: 0 });
    expect(e.designNode).toBe("9:9");
  });
  it("lists failed geometry checks per frame", () => {
    expect(failedBoxChecks({ A: entry(true), B: entry(false) })).toEqual([{ frame: "B", check: expect.objectContaining({ failure: "boom" }) }]);
  });
});
