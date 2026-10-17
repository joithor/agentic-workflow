import { describe, expect, it } from "vitest";
import { saveNote } from "../src/notes/service";
import { createLink, resolveLink, revokeLink } from "../src/sharing/links";

describe("link revocation", () => {
  it("a revoked link stops resolving when the flag is on", () => {
    process.env.FLAGS = "linkRevocation";
    saveNote("ana", { id: "n3", teamId: "t1", title: "Temp", body: "z", createdAt: "2025-01-01", updatedAt: "2025-01-01" });
    const link = createLink("ana", "n3");
    revokeLink("ana", link.id);
    expect(resolveLink(link.token)).toBeUndefined();
  });
});
