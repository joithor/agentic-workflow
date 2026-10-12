-- Stage S-3a: the product graph (context spec §6.1, §6.2). One map_version row per build or import; its graph rows
-- carry its id and go with it. The override layer (map_override) outlives versions: it names targets by key.
CREATE TABLE map_version (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    repo         TEXT    NOT NULL,
    commit_sha   TEXT,
    key_version  INTEGER NOT NULL,
    created      TEXT    NOT NULL,
    status       TEXT    NOT NULL CHECK (status IN ('building', 'draft', 'current', 'retained'))
) STRICT;

CREATE UNIQUE INDEX map_version_one_current ON map_version (status) WHERE status = 'current';

CREATE TABLE node (
    map_version  INTEGER NOT NULL REFERENCES map_version (id) ON DELETE CASCADE,
    key          TEXT    NOT NULL,
    label        TEXT    NOT NULL,
    attributes   TEXT,
    provenance   TEXT    NOT NULL CHECK (provenance IN ('deterministic', 'model', 'human')),
    confidence   REAL    NOT NULL,
    trust        TEXT    NOT NULL CHECK (trust IN ('trusted', 'untrusted')),
    written_at   TEXT,
    PRIMARY KEY (map_version, key)
) STRICT;

CREATE TABLE node_evidence (
    map_version  INTEGER NOT NULL,
    node_key     TEXT    NOT NULL,
    position     INTEGER NOT NULL,
    record       TEXT    NOT NULL,
    span         TEXT,
    PRIMARY KEY (map_version, node_key, position),
    FOREIGN KEY (map_version, node_key) REFERENCES node (map_version, key) ON DELETE CASCADE
) STRICT;

CREATE TABLE node_alias (
    map_version  INTEGER NOT NULL,
    alias        TEXT    NOT NULL,
    target       TEXT    NOT NULL,
    PRIMARY KEY (map_version, alias),
    FOREIGN KEY (map_version, target) REFERENCES node (map_version, key) ON DELETE CASCADE
) STRICT;

CREATE TABLE edge (
    id           INTEGER PRIMARY KEY,
    map_version  INTEGER NOT NULL,
    from_key     TEXT    NOT NULL,
    to_key       TEXT    NOT NULL,
    type         TEXT    NOT NULL,
    provenance   TEXT    NOT NULL CHECK (provenance IN ('deterministic', 'model', 'human')),
    confidence   REAL    NOT NULL,
    trust        TEXT    NOT NULL CHECK (trust IN ('trusted', 'untrusted')),
    step_order   INTEGER,
    written_at   TEXT,
    UNIQUE (map_version, from_key, to_key, type),
    FOREIGN KEY (map_version, from_key) REFERENCES node (map_version, key) ON DELETE CASCADE,
    FOREIGN KEY (map_version, to_key) REFERENCES node (map_version, key) ON DELETE CASCADE
) STRICT;

CREATE INDEX edge_to ON edge (map_version, to_key);

CREATE TABLE edge_evidence (
    edge_id      INTEGER NOT NULL REFERENCES edge (id) ON DELETE CASCADE,
    position     INTEGER NOT NULL,
    record       TEXT    NOT NULL,
    span         TEXT,
    PRIMARY KEY (edge_id, position)
) STRICT;

-- Wording and quote only: the record it quotes, what it decides and supersedes are edges (spec §6.1).
CREATE TABLE decision (
    map_version  INTEGER NOT NULL,
    key          TEXT    NOT NULL,
    quote        TEXT    NOT NULL,
    wording      TEXT    NOT NULL,
    provenance   TEXT    NOT NULL CHECK (provenance IN ('deterministic', 'model', 'human')),
    trust        TEXT    NOT NULL CHECK (trust IN ('trusted', 'untrusted')),
    written_at   TEXT,
    PRIMARY KEY (map_version, key),
    FOREIGN KEY (map_version, key) REFERENCES node (map_version, key) ON DELETE CASCADE
) STRICT;

CREATE TABLE map_override (
    id           TEXT    PRIMARY KEY,
    action       TEXT    NOT NULL CHECK (action IN ('accept', 'reject', 'rename', 'merge', 'move', 'add', 'delete')),
    target       TEXT    NOT NULL,
    label        TEXT,
    other        TEXT,
    who          TEXT    NOT NULL,
    at           TEXT    NOT NULL,
    rejection    TEXT
) STRICT;
