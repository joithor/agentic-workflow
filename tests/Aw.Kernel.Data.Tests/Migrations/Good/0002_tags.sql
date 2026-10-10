CREATE TABLE tags (
  item_id TEXT NOT NULL REFERENCES items (id),
  tag TEXT NOT NULL
);
