import { describe, expect, it } from "vitest";

import { lineDiff } from "../src/profile/approve.js";

describe("lineDiff", () => {
  it("marks removed and added lines and keeps common ones", () => {
    expect(lineDiff(["a", "b", "c"], ["a", "x", "c", "d"])).toEqual(["  a", "- b", "+ x", "  c", "+ d"]);
    expect(lineDiff([], ["n"])).toEqual(["+ n"]);
    expect(lineDiff(["o"], [])).toEqual(["- o"]);
  });
});
