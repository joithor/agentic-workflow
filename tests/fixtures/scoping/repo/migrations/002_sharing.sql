CREATE TABLE shared_links (
  id TEXT PRIMARY KEY, note_id TEXT NOT NULL REFERENCES notes(id), token TEXT NOT NULL UNIQUE,
  created_by TEXT NOT NULL, created_at TEXT NOT NULL
);
