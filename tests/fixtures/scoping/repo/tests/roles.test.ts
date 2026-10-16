import { describe, expect, it } from "vitest";
import { atLeast } from "../src/roles/roles";

describe("roles", () => {
  it("ranks owner over editor over viewer", () => {
    expect(atLeast("owner", "editor")).toBe(true);
    expect(atLeast("viewer", "editor")).toBe(false);
  });
});
