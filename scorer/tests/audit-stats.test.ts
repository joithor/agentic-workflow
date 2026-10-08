import { describe, expect, it } from "vitest";

import { wilson } from "../src/audit/stats.js";

describe("wilson", () => {
  it("returns the uninformative interval for n = 0", () => {
    expect(wilson(0, 0)).toEqual({ rate: 0, lower: 0, upper: 1 });
  });

  it("matches known 95% Wilson bounds", () => {
    const half = wilson(5, 10);
    expect(half.rate).toBe(0.5);
    expect(half.lower).toBeCloseTo(0.2366, 3);
    expect(half.upper).toBeCloseTo(0.7634, 3);
    expect(wilson(10, 10).lower).toBeCloseTo(0.7224, 3);
    expect(wilson(10, 10).upper).toBeCloseTo(1, 6);
    expect(wilson(0, 10).lower).toBeCloseTo(0, 6);
    expect(wilson(0, 10).upper).toBeCloseTo(0.2776, 3);
  });

  it("rejects impossible counts", () => {
    expect(() => wilson(3, 2)).toThrow(RangeError);
    expect(() => wilson(-1, 2)).toThrow(RangeError);
  });
});
