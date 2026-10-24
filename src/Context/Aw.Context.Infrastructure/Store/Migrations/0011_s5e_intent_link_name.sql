-- Stage S-5e: the 'name' link rule (a feature's name or alias in a record's text). SQLite cannot change a CHECK, so intent_link is rebuilt with the wider rule list; rows, key and foreign key stay,
-- and the table's change-stamp triggers (0009) are created again, since dropping the table drops them.
CREATE TABLE intent_link_new (
    record     TEXT NOT NULL REFERENCES intent_record (key) ON DELETE CASCADE,
    target     TEXT NOT NULL,
    rule       TEXT NOT NULL CHECK (rule IN ('issue-key', 'url', 'path', 'identifier', 'quoted-title', 'name')),
    confidence REAL NOT NULL,
    span_hash  TEXT NOT NULL,
    synced_at  TEXT NOT NULL,
    PRIMARY KEY (record, target, rule)
) STRICT;

INSERT INTO intent_link_new (record, target, rule, confidence, span_hash, synced_at) SELECT record, target, rule, confidence, span_hash, synced_at FROM intent_link;
DROP TABLE intent_link;
ALTER TABLE intent_link_new RENAME TO intent_link;

CREATE TRIGGER intent_link_stamp_ai AFTER INSERT ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_link_stamp_au AFTER UPDATE ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_link_stamp_ad AFTER DELETE ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
