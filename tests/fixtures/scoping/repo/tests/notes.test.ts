import { describe, expect, it } from "vitest";
import { getNote, saveNote } from "../src/notes/service";

describe("notes", () => {
  it("saves and reads a note", () => {
    saveNote("ana", { id: "n1", teamId: "t1", title: "Plan", body: "x", createdAt: "2025-01-01", updatedAt: "2025-01-01" });
    expect(getNote("n1")?.title).toBe("Plan");
  });
});
