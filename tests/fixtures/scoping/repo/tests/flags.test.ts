import { describe, expect, it } from "vitest";
import { flags } from "../src/config/flags";

describe("flags", () => {
  it("defaults the digest on", () => {
    expect(flags.digest).toBe(true);
  });
});
