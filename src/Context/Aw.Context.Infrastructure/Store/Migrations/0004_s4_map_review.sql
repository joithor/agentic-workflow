-- Stage S-4: map review (context spec §6.1, §8.7). SQLite can't alter a CHECK list, so map_override is rebuilt: 'action'
-- gains 'propose' and 'undo', and three nullable columns join it: 'link' (the edge a link decision names), 'undoes' (the
-- override an undo reverses) and 'payload' (a proposal's run, repo, kind, parent, evidence and quote, as JSON). Existing
-- rows are copied as they are; nothing references this table.
CREATE TABLE map_override_s4 (
    id           TEXT    PRIMARY KEY,
    action       TEXT    NOT NULL CHECK (action IN ('accept', 'reject', 'rename', 'merge', 'move', 'add', 'delete', 'propose', 'undo')),
    target       TEXT    NOT NULL,
    label        TEXT,
    other        TEXT,
    who          TEXT    NOT NULL,
    at           TEXT    NOT NULL,
    rejection    TEXT,
    link         TEXT,
    undoes       TEXT,
    payload      TEXT
) STRICT;

INSERT INTO map_override_s4 (id, action, target, label, other, who, at, rejection)
SELECT id, action, target, label, other, who, at, rejection FROM map_override;

DROP TABLE map_override;

ALTER TABLE map_override_s4 RENAME TO map_override;
