import { describe, expect, it } from "vitest";
import { exportTeam } from "../src/export/exporter";

describe("export", () => {
  it("renders json", () => {
    expect(exportTeam("empty-team", "json")).toBe("[]");
  });
});
