import { describe, expect, it } from "vitest";

import { GRAPHIFY_PIN, GRAPHIFY_PIN_DATE } from "../src/index/pins.js";

describe("pins", () => {
  it("pins graphify to an exact release and its upload time (set in Task 7 Step 1)", () => {
    expect(GRAPHIFY_PIN).toMatch(/^\d+\.\d+\.\d+$/);
    // The cutoff is the next 00:00Z after the release's last file upload: uv excludes files
    // uploaded at or after it, so the first file's own upload time would exclude the rest.
    expect(GRAPHIFY_PIN_DATE).toMatch(/^\d{4}-\d{2}-\d{2}T00:00:00Z$/);
  });
});
