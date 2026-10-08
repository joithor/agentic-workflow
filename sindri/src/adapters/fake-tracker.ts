import { err, ok, type Result, type StatusKind, type Tracker, type WorkItem } from "./types.js";

// In-memory Tracker for tests in this and later plans. Writes are recorded once
// per idempotency key.
export function makeFakeTracker(seed: WorkItem[]): Tracker & { touch(id: string): void; removeSource(): void; writes: string[] } {
  const items = new Map(seed.map((i) => [i.id, { item: { ...i }, version: 1 }]));
  let version = 1;
  let gone = false;
  const missing = <T>(): Result<T> => err({ kind: "fatal", code: "SND-TRACKER-001", message: "fake source removed" });
  const writes: string[] = [];
  const notFound = <T>(id: string): Result<T> => err({ kind: "not-found", code: "SND-TRACKER-404", message: `no item ${id}` });
  const record = (id: string, entry: string): Result<void> => {
    if (!items.has(id)) return notFound(id);
    if (!writes.includes(entry)) writes.push(entry);
    return ok(undefined);
  };
  return {
    writes,
    touch(id) {
      const e = items.get(id);
      if (e !== undefined) e.version = ++version;
    },
    removeSource() {
      gone = true;
    },
    async scan(scope, cursor) {
      if (gone) return missing();
      const since = Number(cursor ?? "0");
      const refs = [...items.values()]
        .filter((e) => (scope.includeDone || e.item.state === "open") && e.version > since)
        .map((e) => ({ id: e.item.id, updatedAt: e.item.updatedAt }));
      return ok({ items: refs, cursor: String(version) });
    },
    async read(id) {
      if (gone) return missing();
      const e = items.get(id);
      return e === undefined ? notFound(id) : ok({ ...e.item });
    },
    comment: async (id, _body, key) => record(id, `comment:${id}:${key}`),
    setStatus: async (id, status: StatusKind) => record(id, `status:${id}:${status}`),
    assign: async (id, who) => record(id, `assign:${id}:${who}`),
    attach: async (id, _url, _title, key) => record(id, `attach:${id}:${key}`),
  };
}
