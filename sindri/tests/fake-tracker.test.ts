import { describe, expect, it } from "vitest";

import { trackerContractTests } from "./contract/tracker-contract.js";
import { makeFakeTracker } from "../src/adapters/fake-tracker.js";
import { unwrap, type WorkItem } from "../src/adapters/types.js";

const item = (id: string, state: "open" | "done" = "open"): WorkItem => ({
  id, title: `Item ${id}`, body: "body", url: `fake:${id}`, state, authors: [{ id: "a@example.com", role: "creator" }], updatedAt: "2026-10-08T00:00:00Z", meta: {},
});

trackerContractTests("fake", async () => {
  const t = makeFakeTracker([item("F-1"), item("F-2", "done")]);
  return { tracker: t, touch: async (id) => t.touch(id) };
});

describe("fake tracker", () => {
  it("filters done items unless asked, and records idempotent writes", async () => {
    const t = makeFakeTracker([item("F-1"), item("F-2", "done")]);
    const open = await t.scan({ includeDone: false });
    expect(open.ok && open.value.items.map((i) => i.id)).toEqual(["F-1"]);
    await t.comment("F-1", "hi", "k1");
    await t.comment("F-1", "hi", "k1");
    await t.attach("F-1", "https://x", "x", "k2");
    await t.setStatus("F-1", "in-progress");
    await t.assign("F-1", "self");
    expect(t.writes).toEqual(["comment:F-1:k1", "attach:F-1:k2", "status:F-1:in-progress", "assign:F-1:self"]);
    expect((await t.comment("nope", "x", "k")).ok).toBe(false);
  });

  it("unwrap returns values and throws typed errors", async () => {
    const t = makeFakeTracker([item("F-1")]);
    expect(unwrap(await t.read("F-1")).id).toBe("F-1");
    expect(() => unwrap({ ok: false, error: { kind: "not-found", code: "SND-TRACKER-404", message: "no item nope" } })).toThrow("no item nope");
  });
});
