import type { Scrubber } from "../scrub/scrub.js";
import type { Ledger } from "./db.js";

export interface ObservedItem {
  id: string;
  source: string;
  title: string;
  state: "open" | "done";
  size: string | null;
  sizedBy: string | null;
  ambiguity: string | null;
  stepsDone: number;
  stepsTotal: number;
  contentHash: string;
}

export interface WriteCtx {
  epoch: number;
  tickId: string;
  now: Date;
  scrubber: Scrubber;
}

export interface ItemRow {
  id: string;
  source: string;
  title: string;
  state: string;
  size: string | null;
  sized_by: string | null;
  ambiguity: string | null;
  steps_done: number;
  steps_total: number;
  content_hash: string;
  first_seen: string;
  last_seen: string;
  epoch: number;
}

export interface EventRow {
  seq: number;
  source: string;
  item_id: string;
  ts: string;
  kind: string;
  detail: string;
  epoch: number;
  tick_id: string;
}

const TITLE_CAP = 200;
const DETAIL_CAP = 2000;

function event(db: Ledger, ctx: WriteCtx, source: string, itemId: string, kind: string, detail: Record<string, unknown>): void {
  const text = JSON.stringify(ctx.scrubber.scrubDeep(detail)).slice(0, DETAIL_CAP);
  db.prepare("INSERT INTO item_events (source, item_id, ts, kind, detail, epoch, tick_id) VALUES (?, ?, ?, ?, ?, ?, ?)").run(
    source, itemId, ctx.now.toISOString(), kind, text, ctx.epoch, ctx.tickId,
  );
}

// Call inside withEpoch(): the caller owns the transaction and the fencing check.
export function upsertItem(db: Ledger, ctx: WriteCtx, item: ObservedItem): "new" | "changed" | "same" {
  const now = ctx.now.toISOString();
  const title = ctx.scrubber.scrub(item.title).text.slice(0, TITLE_CAP);
  const prev = db.prepare("SELECT state, content_hash FROM items WHERE source = ? AND id = ?").get(item.source, item.id) as
    | { state: string; content_hash: string }
    | undefined;
  if (prev === undefined) {
    db.prepare(
      `INSERT INTO items (id, source, title, state, size, sized_by, ambiguity, steps_done, steps_total, content_hash, first_seen, last_seen, epoch)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
    ).run(item.id, item.source, title, item.state, item.size, item.sizedBy, item.ambiguity, item.stepsDone, item.stepsTotal, item.contentHash, now, now, ctx.epoch);
    event(db, ctx, item.source, item.id, "seen", { state: item.state, size: item.size });
    return "new";
  }
  if (prev.content_hash === item.contentHash && prev.state === item.state) {
    db.prepare("UPDATE items SET last_seen = ?, epoch = ? WHERE source = ? AND id = ?").run(now, ctx.epoch, item.source, item.id);
    return "same";
  }
  db.prepare(
    `UPDATE items SET title = ?, state = ?, size = ?, sized_by = ?, ambiguity = ?, steps_done = ?, steps_total = ?,
       content_hash = ?, last_seen = ?, epoch = ? WHERE source = ? AND id = ?`,
  ).run(title, item.state, item.size, item.sizedBy, item.ambiguity, item.stepsDone, item.stepsTotal, item.contentHash, now, ctx.epoch, item.source, item.id);
  if (prev.state !== item.state) event(db, ctx, item.source, item.id, "state-changed", { from: prev.state, to: item.state });
  else event(db, ctx, item.source, item.id, "changed", {});
  return "changed";
}

// Items a full scan no longer returns (a task deleted or renumbered) are closed
// as "removed", so the ledger never shows ghosts as open work.
export function markMissing(db: Ledger, ctx: WriteCtx, source: string, seen: ReadonlySet<string>): number {
  const rows = db.prepare("SELECT id, state FROM items WHERE source = ? AND state != 'removed'").all(source) as { id: string; state: string }[];
  const gone = rows.filter((r) => !seen.has(r.id));
  for (const r of gone) {
    db.prepare("UPDATE items SET state = 'removed', last_seen = ?, epoch = ? WHERE source = ? AND id = ?").run(ctx.now.toISOString(), ctx.epoch, source, r.id);
    event(db, ctx, source, r.id, "removed", { from: r.state });
  }
  return gone.length;
}

export function listItems(db: Ledger, filter: { state?: "open" | "done" } = {}): ItemRow[] {
  if (filter.state !== undefined) return db.prepare("SELECT * FROM items WHERE state = ? ORDER BY source, id").all(filter.state) as ItemRow[];
  return db.prepare("SELECT * FROM items ORDER BY source, id").all() as ItemRow[];
}

// The newest `limit` matching events, oldest first.
export function listEvents(db: Ledger, filter: { itemId?: string; since?: Date; limit?: number }): EventRow[] {
  return db
    .prepare(
      `SELECT * FROM (SELECT * FROM item_events
       WHERE (@itemId IS NULL OR item_id = @itemId) AND (@since IS NULL OR ts >= @since)
       ORDER BY seq DESC LIMIT @limit) ORDER BY seq`,
    )
    .all({ itemId: filter.itemId ?? null, since: filter.since?.toISOString() ?? null, limit: filter.limit ?? 1000 }) as EventRow[];
}

export function getCursor(db: Ledger, source: string): string | null {
  const row = db.prepare("SELECT cursor FROM cursors WHERE source = ?").get(source) as { cursor: string } | undefined;
  return row?.cursor ?? null;
}

export function setCursor(db: Ledger, source: string, cursor: string, now: Date): void {
  db.prepare(
    "INSERT INTO cursors (source, cursor, updated_at) VALUES (?, ?, ?) ON CONFLICT(source) DO UPDATE SET cursor = excluded.cursor, updated_at = excluded.updated_at",
  ).run(source, cursor, now.toISOString());
}
