-- The read side (API spec §2.3): the projector's cursor and state (one row), and one table per view keyed by its id,
-- each row a Core record as JSON text with the ledger sequence that last wrote it. views.db is derived: deleting it
-- rebuilds it from the ledger.
CREATE TABLE projector_state (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    cursor INTEGER NOT NULL CHECK (cursor >= 0),
    schema_version INTEGER NOT NULL,
    projected_at TEXT,
    rebuilding INTEGER NOT NULL DEFAULT 0 CHECK (rebuilding IN (0, 1)),
    stalled_at INTEGER,
    stalled_type TEXT
);

CREATE TABLE view_services (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);

CREATE TABLE view_profile (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
