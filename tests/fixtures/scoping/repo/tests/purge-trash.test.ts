import { describe, expect, it } from "vitest";
import { purgeTrash } from "../src/jobs/purge-trash";

describe("purge trash", () => {
  it("purges nothing from an empty team", () => {
    expect(purgeTrash("empty-team", new Date())).toBe(0);
  });
});
