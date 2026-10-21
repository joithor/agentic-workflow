-- Stage S-5c, continued: the intent layer gets its own counter, so an intent sync no longer moves the map's (0008's change_stamp), which made
-- every read after a sync load the whole map again and rebuild its view. 0008's intent triggers are replaced by ones that move stamp_intent.
CREATE TABLE stamp_intent (
    id   INTEGER PRIMARY KEY CHECK (id = 1),
    seq  INTEGER NOT NULL
) STRICT;

INSERT INTO stamp_intent (id, seq) VALUES (1, 0);

DROP TRIGGER intent_record_stamp_ai;
CREATE TRIGGER intent_record_stamp_ai AFTER INSERT ON intent_record BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_record_stamp_au;
CREATE TRIGGER intent_record_stamp_au AFTER UPDATE ON intent_record BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_record_stamp_ad;
CREATE TRIGGER intent_record_stamp_ad AFTER DELETE ON intent_record BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_link_stamp_ai;
CREATE TRIGGER intent_link_stamp_ai AFTER INSERT ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_link_stamp_au;
CREATE TRIGGER intent_link_stamp_au AFTER UPDATE ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_link_stamp_ad;
CREATE TRIGGER intent_link_stamp_ad AFTER DELETE ON intent_link BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_decision_stamp_ai;
CREATE TRIGGER intent_decision_stamp_ai AFTER INSERT ON intent_decision BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_decision_stamp_au;
CREATE TRIGGER intent_decision_stamp_au AFTER UPDATE ON intent_decision BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_decision_stamp_ad;
CREATE TRIGGER intent_decision_stamp_ad AFTER DELETE ON intent_decision BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_edge_stamp_ai;
CREATE TRIGGER intent_edge_stamp_ai AFTER INSERT ON intent_edge BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_edge_stamp_au;
CREATE TRIGGER intent_edge_stamp_au AFTER UPDATE ON intent_edge BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_edge_stamp_ad;
CREATE TRIGGER intent_edge_stamp_ad AFTER DELETE ON intent_edge BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_conflict_stamp_ai;
CREATE TRIGGER intent_conflict_stamp_ai AFTER INSERT ON intent_conflict BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_conflict_stamp_au;
CREATE TRIGGER intent_conflict_stamp_au AFTER UPDATE ON intent_conflict BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
DROP TRIGGER intent_conflict_stamp_ad;
CREATE TRIGGER intent_conflict_stamp_ad AFTER DELETE ON intent_conflict BEGIN UPDATE stamp_intent SET seq = seq + 1 WHERE id = 1; END;
