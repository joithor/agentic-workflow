-- The meta harness schema (C# build: fresh state, nothing is read from another build).
CREATE TABLE messages (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  id TEXT NOT NULL UNIQUE,
  from_socket TEXT NOT NULL,
  from_name TEXT NOT NULL,
  body TEXT NOT NULL,
  at TEXT NOT NULL,
  kind TEXT NOT NULL CHECK (kind IN ('Request', 'OwnerQuestion', 'Ready', 'LaneAnnounce')),
  pr INTEGER,
  lane TEXT,
  resolution TEXT CHECK (resolution IN ('Dismissed', 'Deferred')),
  resolution_reason TEXT,
  resolution_until TEXT
);
CREATE INDEX messages_at ON messages(at, seq);

CREATE TABLE outbound (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  msg_id TEXT,
  to_peer TEXT NOT NULL,
  message TEXT NOT NULL,
  at TEXT NOT NULL
);

CREATE TABLE obligations (
  kind TEXT NOT NULL,
  key TEXT NOT NULL,
  state TEXT NOT NULL,
  note TEXT,
  at TEXT NOT NULL,
  PRIMARY KEY (kind, key)
);

CREATE TABLE offsets (
  transcript TEXT PRIMARY KEY,
  byte_offset INTEGER NOT NULL
);

CREATE TABLE role (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  session_id TEXT NOT NULL,
  name TEXT NOT NULL
);

CREATE TABLE lanes (
  position INTEGER PRIMARY KEY,
  lane TEXT NOT NULL,
  session_name TEXT NOT NULL,
  session_id TEXT,
  source TEXT NOT NULL CHECK (source IN ('Direct', 'Announcement'))
);

CREATE TABLE lane_map_versions (
  version INTEGER PRIMARY KEY,
  at TEXT NOT NULL
);

CREATE TABLE gates (
  pr INTEGER NOT NULL,
  run INTEGER NOT NULL,
  head TEXT NOT NULL,
  main TEXT NOT NULL,
  scopes TEXT NOT NULL,
  verdict TEXT NOT NULL CHECK (verdict IN ('Pass', 'Fail', 'Conflict')),
  at TEXT NOT NULL,
  PRIMARY KEY (pr, run)
);

CREATE TABLE gate_steps (
  pr INTEGER NOT NULL,
  run INTEGER NOT NULL,
  position INTEGER NOT NULL,
  name TEXT NOT NULL,
  verdict TEXT NOT NULL CHECK (verdict IN ('Pass', 'Fail', 'Conflict')),
  log TEXT NOT NULL,
  flake TEXT CHECK (flake IN ('Flake', 'Real')),
  PRIMARY KEY (pr, run, position),
  FOREIGN KEY (pr, run) REFERENCES gates(pr, run)
);

CREATE TABLE merges (
  pr INTEGER NOT NULL,
  sha TEXT NOT NULL,
  at TEXT NOT NULL,
  PRIMARY KEY (pr, sha)
);

CREATE TABLE owner_questions (
  number INTEGER PRIMARY KEY AUTOINCREMENT,
  asked_by TEXT NOT NULL,
  text TEXT NOT NULL,
  asked_at TEXT NOT NULL,
  answer TEXT,
  answered_at TEXT
);

CREATE TABLE kv (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
