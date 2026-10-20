-- Stage S-5c (named for the intent layer whose reads it serves with the map's; the loader wants a numbered stage): one counter that every write to a table a map read loads
-- moves, so the store can keep the snapshot and intent layer it loaded, and the reader the view it built, until the counter moves. Triggers run per
-- row in the writer's own transaction, so a reader that reads the counter in its read transaction gets the number that matches what it loaded.
CREATE TABLE change_stamp (
    id   INTEGER PRIMARY KEY CHECK (id = 1),
    seq  INTEGER NOT NULL
) STRICT;

INSERT INTO change_stamp (id, seq) VALUES (1, 0);

CREATE TRIGGER map_version_stamp_ai AFTER INSERT ON map_version BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER map_version_stamp_au AFTER UPDATE ON map_version BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER map_version_stamp_ad AFTER DELETE ON map_version BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_stamp_ai AFTER INSERT ON node BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_stamp_au AFTER UPDATE ON node BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_stamp_ad AFTER DELETE ON node BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_stamp_ai AFTER INSERT ON edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_stamp_au AFTER UPDATE ON edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_stamp_ad AFTER DELETE ON edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_alias_stamp_ai AFTER INSERT ON node_alias BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_alias_stamp_au AFTER UPDATE ON node_alias BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_alias_stamp_ad AFTER DELETE ON node_alias BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_evidence_stamp_ai AFTER INSERT ON node_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_evidence_stamp_au AFTER UPDATE ON node_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER node_evidence_stamp_ad AFTER DELETE ON node_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_evidence_stamp_ai AFTER INSERT ON edge_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_evidence_stamp_au AFTER UPDATE ON edge_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER edge_evidence_stamp_ad AFTER DELETE ON edge_evidence BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER decision_stamp_ai AFTER INSERT ON decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER decision_stamp_au AFTER UPDATE ON decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER decision_stamp_ad AFTER DELETE ON decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER map_override_stamp_ai AFTER INSERT ON map_override BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER map_override_stamp_au AFTER UPDATE ON map_override BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER map_override_stamp_ad AFTER DELETE ON map_override BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_record_stamp_ai AFTER INSERT ON intent_record BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_record_stamp_au AFTER UPDATE ON intent_record BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_record_stamp_ad AFTER DELETE ON intent_record BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_link_stamp_ai AFTER INSERT ON intent_link BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_link_stamp_au AFTER UPDATE ON intent_link BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_link_stamp_ad AFTER DELETE ON intent_link BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_decision_stamp_ai AFTER INSERT ON intent_decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_decision_stamp_au AFTER UPDATE ON intent_decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_decision_stamp_ad AFTER DELETE ON intent_decision BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_edge_stamp_ai AFTER INSERT ON intent_edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_edge_stamp_au AFTER UPDATE ON intent_edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_edge_stamp_ad AFTER DELETE ON intent_edge BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_conflict_stamp_ai AFTER INSERT ON intent_conflict BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_conflict_stamp_au AFTER UPDATE ON intent_conflict BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER intent_conflict_stamp_ad AFTER DELETE ON intent_conflict BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
