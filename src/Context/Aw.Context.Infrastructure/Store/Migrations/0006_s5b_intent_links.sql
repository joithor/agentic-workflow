-- Stage S-5B: the intent layer (context spec §6.2, S-5B). What syncs wrote, outside map_version so a build or promote never drops it.
-- A record's text is never stored: only its key, version, scrubbed one-line title and aliases (newline-separated). Decisions keep a verbatim quote
-- and Sindri's wording, both scrubbed at the ingest boundary. Trust is computed on read (records and model text untrusted), never stored.
CREATE TABLE intent_record (
    key              TEXT PRIMARY KEY,
    source           TEXT NOT NULL,
    version          TEXT NOT NULL,
    title            TEXT NOT NULL,
    aliases          TEXT NOT NULL,
    fetched_at       TEXT NOT NULL,
    extracted_version TEXT,
    extraction_state TEXT NOT NULL CHECK (extraction_state IN ('queued', 'done', 'none'))
) STRICT;

CREATE INDEX intent_record_by_source ON intent_record (source);

CREATE TABLE intent_link (
    record     TEXT NOT NULL REFERENCES intent_record (key) ON DELETE CASCADE,
    target     TEXT NOT NULL,
    rule       TEXT NOT NULL CHECK (rule IN ('issue-key', 'url', 'path', 'identifier', 'quoted-title')),
    confidence REAL NOT NULL,
    span_hash  TEXT NOT NULL,
    synced_at  TEXT NOT NULL,
    PRIMARY KEY (record, target, rule)
) STRICT;

CREATE TABLE intent_decision (
    key        TEXT PRIMARY KEY,
    record     TEXT NOT NULL REFERENCES intent_record (key) ON DELETE CASCADE,
    quote      TEXT NOT NULL,
    wording    TEXT NOT NULL,
    provenance TEXT NOT NULL CHECK (provenance IN ('deterministic', 'model', 'human')),
    at         TEXT NOT NULL
) STRICT;

CREATE INDEX intent_decision_by_record ON intent_decision (record);

CREATE TABLE intent_edge (
    from_key   TEXT NOT NULL,
    to_key     TEXT NOT NULL,
    type       TEXT NOT NULL CHECK (type IN ('decides', 'supersedes')),
    provenance TEXT NOT NULL CHECK (provenance IN ('deterministic', 'model', 'human')),
    confidence REAL NOT NULL,
    at         TEXT NOT NULL,
    PRIMARY KEY (from_key, to_key, type)
) STRICT;

CREATE INDEX intent_edge_by_to ON intent_edge (to_key);

CREATE TABLE intent_conflict (
    a       TEXT NOT NULL,
    b       TEXT NOT NULL,
    verdict TEXT NOT NULL CHECK (verdict IN ('contradicts', 'agrees', 'supersedes')),
    at      TEXT NOT NULL,
    PRIMARY KEY (a, b),
    CHECK (a < b)
) STRICT;

CREATE INDEX intent_conflict_by_b ON intent_conflict (b);
