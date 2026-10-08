import { describe, expect, it } from "vitest";

import { autoStartShare } from "../src/audit/items.js";

describe("autoStartShare", () => {
  it("counts items at or below maxSize that are unambiguous and fully trusted", () => {
    const r = autoStartShare([
      { id: "a", size: "XS", ambiguous: false, authorsTrusted: true },
      { id: "b", size: "S", ambiguous: false, authorsTrusted: true },
      { id: "c", size: "XS", ambiguous: true, authorsTrusted: true },
      { id: "d", size: "XS", ambiguous: false, authorsTrusted: false },
      { id: "e" },
    ], "XS");
    expect(r).toEqual({ eligible: 1, total: 5, share: 0.2 });
  });

  it("returns share 0 for an empty list", () => {
    expect(autoStartShare([], "XS")).toEqual({ eligible: 0, total: 0, share: 0 });
  });

  it("includes larger sizes up to maxSize and treats an undefined maxSize as nothing eligible", () => {
    const items = [{ id: "a", size: "S" as const, ambiguous: false, authorsTrusted: true }];
    expect(autoStartShare(items, "M").eligible).toBe(1);
    expect(autoStartShare(items, undefined).eligible).toBe(0);
  });
});
