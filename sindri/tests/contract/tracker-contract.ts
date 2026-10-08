import { describe, expect, it } from "vitest";

import type { Result, Tracker } from "../../src/adapters/types.js";

export interface TrackerFixture {
  tracker: Tracker;
  touch(id: string): Promise<void>;
  // Makes the tracker's source unreachable (for plan-file: delete the plan dir).
  removeSource(): Promise<void>;
}

const outcome = (r: Result<void>): string => (r.ok ? "ok" : `${r.error.kind}:${r.error.code}`);

// Every Tracker (built-in or fake) must pass this (spec §11.2). The fixture must
// hold at least one open item.
export function trackerContractTests(name: string, make: () => Promise<TrackerFixture>): void {
  describe(`Tracker contract: ${name}`, () => {
    it("scan returns unique ids and a cursor; an unchanged rescan returns nothing", async () => {
      const { tracker } = await make();
      const first = await tracker.scan({ includeDone: true });
      if (!first.ok) throw new Error(first.error.message);
      const ids = first.value.items.map((i) => i.id);
      expect(ids.length).toBeGreaterThan(0);
      expect(new Set(ids).size).toBe(ids.length);
      const again = await tracker.scan({ includeDone: true }, first.value.cursor);
      expect(again.ok && again.value.items).toEqual([]);
    });

    it("read returns each scanned item, with authors", async () => {
      const { tracker } = await make();
      const scan = await tracker.scan({ includeDone: true });
      if (!scan.ok) throw new Error(scan.error.message);
      for (const ref of scan.value.items) {
        const r = await tracker.read(ref.id);
        expect(r.ok && r.value.id).toBe(ref.id);
        expect(r.ok && Array.isArray(r.value.authors)).toBe(true);
      }
    });

    it("read of an unknown id is not-found", async () => {
      const { tracker } = await make();
      const r = await tracker.read("no-such-item-0000");
      expect(r.ok).toBe(false);
      if (!r.ok) expect(r.error.kind).toBe("not-found");
    });

    it("a touched item shows up in the next scan", async () => {
      const { tracker, touch } = await make();
      const first = await tracker.scan({ includeDone: false });
      if (!first.ok) throw new Error(first.error.message);
      const target = first.value.items[0].id;
      await touch(target);
      const next = await tracker.scan({ includeDone: false }, first.value.cursor);
      expect(next.ok && next.value.items.map((i) => i.id)).toContain(target);
    });

    it("a missing source is a fatal error, never an empty backlog", async () => {
      const { tracker, removeSource } = await make();
      const first = await tracker.scan({ includeDone: true });
      if (!first.ok) throw new Error(first.error.message);
      await removeSource();
      const scan = await tracker.scan({ includeDone: true }, first.value.cursor);
      expect(scan.ok).toBe(false);
      if (!scan.ok) expect(scan.error).toMatchObject({ kind: "fatal", code: "SND-TRACKER-001" });
      const read = await tracker.read(first.value.items[0].id);
      expect(read.ok).toBe(false);
      if (!read.ok) expect(read.error.kind).toBe("fatal");
    });

    it("writes are idempotent: the same call twice has the same outcome", async () => {
      const { tracker } = await make();
      const scan = await tracker.scan({ includeDone: false });
      if (!scan.ok) throw new Error(scan.error.message);
      const id = scan.value.items[0].id;
      const calls: (() => Promise<Result<void>>)[] = [
        () => tracker.comment(id, "status", "contract-key"),
        () => tracker.setStatus(id, "in-progress"),
        () => tracker.assign(id, "self"),
        () => tracker.attach(id, "https://example.com/x", "x", "contract-key"),
      ];
      for (const call of calls) expect(outcome(await call())).toBe(outcome(await call()));
    });
  });
}
