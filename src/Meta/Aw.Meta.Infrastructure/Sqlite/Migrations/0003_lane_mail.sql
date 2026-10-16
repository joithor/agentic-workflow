-- Messages the meta sends to non-Claude lanes (lane-send) and the per-lane counter behind the ids of their replies (lane-reply).
-- A reply itself is a row in messages (id lane:<lane>:<n>, from <team>-<lane>), so the meta's stop-check derives its obligation from it.
CREATE TABLE lane_mail (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  lane TEXT NOT NULL,
  text TEXT NOT NULL,
  at TEXT NOT NULL,
  delivered_at TEXT,
  settled_at TEXT
);
CREATE INDEX lane_mail_owed ON lane_mail(lane, settled_at);

CREATE TABLE lane_replies (
  lane TEXT PRIMARY KEY,
  n INTEGER NOT NULL
);
