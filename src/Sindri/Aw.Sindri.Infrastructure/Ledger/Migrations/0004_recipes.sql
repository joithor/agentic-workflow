-- The recipe registry (engine spec §4.1, §5.9): the registered recipes by hash, and their approvals. The outbox is
-- version 3 (S1-D9), so Sindri's own files are 4 and up.
--
-- recipes: the normalized text kept by its hash, with the (id, version) it is approved under. One hash per id and
-- version (SND-RECIPE-007). A generated recipe names its base (both columns, or neither).
CREATE TABLE recipes (
    hash TEXT PRIMARY KEY,
    id TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version >= 1),
    text TEXT NOT NULL,
    base_id TEXT,
    base_hash TEXT,
    registered_at TEXT NOT NULL,
    UNIQUE (id, version),
    CHECK ((base_id IS NULL) = (base_hash IS NULL))
);
-- recipe_approvals: append-only rows keyed by (id, version, hash). The first approval of a key, then one row per drift
-- approval with its new pins; the latest row (highest seq) is the one that counts. blocks_json maps each block id the
-- recipe names to its descriptor hash, children_json each child recipe id to the hash pinned; both are JSON objects in
-- id order (W11). A revoke stamps every row of the hash (revoked_by, revoked_at, and the base whose revoke cascaded,
-- if any); a revoked hash takes no new row (SND-RECIPE-005).
CREATE TABLE recipe_approvals (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    id TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version >= 1),
    hash TEXT NOT NULL,
    approver TEXT NOT NULL,
    approved_at TEXT NOT NULL,
    via TEXT NOT NULL CHECK (via IN ('desk', 'freeze')),
    base_id TEXT,
    base_hash TEXT,
    blocks_json TEXT NOT NULL CHECK (json_valid(blocks_json) AND json_type(blocks_json) = 'object'),
    children_json TEXT NOT NULL CHECK (json_valid(children_json) AND json_type(children_json) = 'object'),
    revoked_by TEXT,
    revoked_at TEXT,
    cascaded_from TEXT,
    CHECK ((base_id IS NULL) = (base_hash IS NULL)),
    CHECK ((revoked_by IS NULL) = (revoked_at IS NULL)),
    CHECK (cascaded_from IS NULL OR revoked_at IS NOT NULL)
);
CREATE INDEX recipe_approvals_key ON recipe_approvals (id, version, hash, seq);
CREATE INDEX recipe_approvals_hash ON recipe_approvals (hash);
CREATE INDEX recipe_approvals_base ON recipe_approvals (base_hash) WHERE base_hash IS NOT NULL;
