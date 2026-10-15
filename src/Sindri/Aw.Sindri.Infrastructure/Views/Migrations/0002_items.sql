-- API P3's item views, the same shape as 0001's: a key, the row as JSON text and the ledger sequence that last wrote it.
-- Schema version 2 (ViewSchema.Version): a views.db written at version 1 gains these tables here and the projector
-- rebuilds it from the ledger, because the version differs.
CREATE TABLE view_items (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    owner TEXT   -- reserved for multi-user (README X61); null for now. The row's own JSON is what readers use.
);

CREATE TABLE view_item_aliases (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);

CREATE TABLE view_item_timeline (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
