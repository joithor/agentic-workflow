CREATE TABLE notes (
  id TEXT PRIMARY KEY, team_id TEXT NOT NULL, title TEXT NOT NULL, body TEXT NOT NULL,
  created_at TEXT NOT NULL, updated_at TEXT NOT NULL
);
