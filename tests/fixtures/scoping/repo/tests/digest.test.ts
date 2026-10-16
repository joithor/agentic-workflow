import { describe, expect, it } from "vitest";
import { sendDigest } from "../src/digest/digest";

describe("digest", () => {
  it("sends nothing when no note changed", async () => {
    expect(await sendDigest("empty-team", ["a@x.invalid"])).toBe(0);
  });
});
