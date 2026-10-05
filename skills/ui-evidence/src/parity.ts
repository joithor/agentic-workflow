// Pure design-parity primitives: crop, pixel-compare, side-by-side, geometry.
// No browser, no network, no filesystem — PNGs are in memory so every branch
// is unit-testable against tiny fixtures.
import pixelmatch from "pixelmatch";
import { PNG } from "pngjs";

import type { Box } from "./pixel-diff.js";

export interface Point { x: number; y: number }

export interface CompareOptions {
  /** pixelmatch per-pixel colour tolerance (0..1). Default 0.1. */
  threshold?: number;
  /** Count anti-aliased pixels as differences. Default false. */
  includeAA?: boolean;
}

export interface RegionComparison {
  diff: PNG;
  diffPixels: number;
  total: number;
  /** Percentage of differing pixels, two decimals. */
  diffPercent: number;
}

/** Copies `region` out of `png`. Throws when it is not wholly inside the image — a silent clip would read as a design difference. */
export function cropRegion(png: PNG, region: Box): PNG {
  const { x, y, w, h } = region;
  if (![x, y, w, h].every(Number.isInteger) || w <= 0 || h <= 0 || x < 0 || y < 0 || x + w > png.width || y + h > png.height) {
    throw new RangeError(`region ${x},${y} ${w}x${h} is outside the ${png.width}x${png.height} image`);
  }
  const out = new PNG({ width: w, height: h });
  PNG.bitblt(png, out, x, y, w, h, 0, 0);
  return out;
}

/** Where to cut the implementation screenshot: the anchor's bbox origin plus `offset`, at the design region's size. */
export function implementationRegion(anchorBox: Box, designRegion: Box, offset: Point = { x: 0, y: 0 }): Box {
  return { x: Math.round(anchorBox.x + offset.x), y: Math.round(anchorBox.y + offset.y), w: designRegion.w, h: designRegion.h };
}

export function compareRegion(design: PNG, implementation: PNG, opts: CompareOptions = {}): RegionComparison {
  if (design.width !== implementation.width || design.height !== implementation.height) {
    throw new RangeError(`size mismatch: design ${design.width}x${design.height} vs implementation ${implementation.width}x${implementation.height}`);
  }
  const { width, height } = design;
  const diff = new PNG({ width, height });
  const diffPixels = pixelmatch(design.data, implementation.data, diff.data, width, height, {
    threshold: opts.threshold ?? 0.1,
    includeAA: opts.includeAA ?? false,
  });
  const total = width * height;
  return { diff, diffPixels, total, diffPercent: +((diffPixels / total) * 100).toFixed(2) };
}

/** Design | implementation | diff on white, separated by `gap` px. */
export function sideBySide(design: PNG, implementation: PNG, diff: PNG, gap = 12): PNG {
  const width = design.width + implementation.width + diff.width + gap * 2;
  const height = Math.max(design.height, implementation.height, diff.height);
  const out = new PNG({ width, height });
  out.data.fill(255);
  PNG.bitblt(design, out, 0, 0, design.width, design.height, 0, 0);
  PNG.bitblt(implementation, out, 0, 0, implementation.width, implementation.height, design.width + gap, 0);
  PNG.bitblt(diff, out, 0, 0, diff.width, diff.height, design.width + implementation.width + gap * 2, 0);
  return out;
}

// ---- geometry assertions ----------------------------------------------------

export type Selector = { testId: string } | { text: string };

export interface BoxExpectation {
  target: Selector;
  /** Measure x/y relative to this element's top-left instead of the page. */
  relativeTo?: Selector;
  expected: { w?: number; h?: number; x?: number; y?: number };
  /** Allowed absolute deviation per axis in px. Default 0. */
  tolerance?: number;
}

export interface BoxCheck {
  target: string;
  relativeTo: string | null;
  expected: { w?: number; h?: number; x?: number; y?: number };
  actual: { w: number; h: number; x: number; y: number } | null;
  tolerance: number;
  pass: boolean;
  /** Human-readable reason when `pass` is false. */
  failure?: string;
}

export function selectorKey(s: Selector): string {
  return "testId" in s ? `testId:${s.testId}` : `text:${s.text}`;
}

const AXES = ["w", "h", "x", "y"] as const;

/** Compares each expectation with the measured boxes. A missing element fails — it is never skipped. */
export function checkBoxes(expectations: BoxExpectation[], measure: (s: Selector) => Box | null): BoxCheck[] {
  return expectations.map((e) => {
    const tolerance = e.tolerance ?? 0;
    const target = selectorKey(e.target);
    const relativeTo = e.relativeTo === undefined ? null : selectorKey(e.relativeTo);
    const base = { target, relativeTo, expected: e.expected, tolerance };
    const box = measure(e.target);
    const origin = e.relativeTo === undefined ? { x: 0, y: 0 } : measure(e.relativeTo);
    if (box === null) return { ...base, actual: null, pass: false, failure: `${target} not found` };
    if (origin === null) return { ...base, actual: null, pass: false, failure: `${relativeTo} not found` };
    const actual = { w: box.w, h: box.h, x: box.x - origin.x, y: box.y - origin.y };
    const misses = AXES.flatMap((axis) => {
      const want = e.expected[axis];
      return want !== undefined && Math.abs(actual[axis] - want) > tolerance ? [`${axis} ${actual[axis]} != ${want}`] : [];
    });
    return misses.length === 0 ? { ...base, actual, pass: true } : { ...base, actual, pass: false, failure: misses.join(", ") };
  });
}

// ---- parity.json ------------------------------------------------------------

export interface ParityEntry {
  designRegion: Box;
  implementationOrigin: Point;
  anchorBox: Box;
  anchorOffset: Point;
  /** Anchor bbox size minus design region size; non-zero means the layout itself differs. */
  sizeDelta: { w: number; h: number };
  diffPixels: number;
  total: number;
  diffPercent: number;
  threshold: number;
  includeAA: boolean;
  boxChecks: BoxCheck[];
  /** Intentional differences that explain a non-zero diff; carried into Linear and PR text. */
  knownDifferences: string[];
  files: { design: string; implementation: string; diff: string; sideBySide: string };
}

export function buildParityEntry(input: {
  designRegion: Box;
  anchorBox: Box;
  anchorOffset: Point;
  comparison: Pick<RegionComparison, "diffPixels" | "total" | "diffPercent">;
  threshold: number;
  includeAA: boolean;
  boxChecks: BoxCheck[];
  knownDifferences: string[];
  files: ParityEntry["files"];
}): ParityEntry {
  const origin = implementationRegion(input.anchorBox, input.designRegion, input.anchorOffset);
  return {
    designRegion: input.designRegion,
    implementationOrigin: { x: origin.x, y: origin.y },
    anchorBox: input.anchorBox,
    anchorOffset: input.anchorOffset,
    sizeDelta: { w: input.anchorBox.w - input.designRegion.w, h: input.anchorBox.h - input.designRegion.h },
    ...input.comparison,
    threshold: input.threshold,
    includeAA: input.includeAA,
    boxChecks: input.boxChecks,
    knownDifferences: input.knownDifferences,
    files: input.files,
  };
}

export function failedBoxChecks(entries: Record<string, ParityEntry>): Array<{ frame: string; check: BoxCheck }> {
  return Object.entries(entries).flatMap(([frame, e]) => e.boxChecks.filter((c) => !c.pass).map((check) => ({ frame, check })));
}
