import { describe, expect, it } from "vitest";

import { ulid } from "../src/ids.js";

describe("ulid", () => {
  it("is 26 lowercase Crockford base32 characters", () => {
    expect(ulid()).toMatch(/^[0-9a-hjkmnp-tv-z]{26}$/);
  });

  it("sorts by time", () => {
    const a = ulid(new Date("2026-01-01T00:00:00Z"));
    const b = ulid(new Date("2026-01-02T00:00:00Z"));
    expect(a < b).toBe(true);
  });
});
