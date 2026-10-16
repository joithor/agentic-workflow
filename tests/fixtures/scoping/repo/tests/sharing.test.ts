import { describe, expect, it } from "vitest";
import { saveNote } from "../src/notes/service";
import { createLink, resolveLink } from "../src/sharing/links";

describe("sharing", () => {
  it("resolves a link to its note without a login", () => {
    saveNote("ana", { id: "n2", teamId: "t1", title: "Shared", body: "y", createdAt: "2025-01-01", updatedAt: "2025-01-01" });
    const link = createLink("ana", "n2");
    expect(resolveLink(link.token)?.id).toBe("n2");
  });

  it("an unknown token resolves to nothing", () => {
    expect(resolveLink("nope")).toBeUndefined();
  });
});
