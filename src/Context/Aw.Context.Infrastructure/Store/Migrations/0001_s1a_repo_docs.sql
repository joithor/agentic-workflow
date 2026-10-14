-- Stage S-1A: the repo-docs snapshot (context spec §6.2). Doc chunks hold text that was sanitized and scrubbed
-- before it was written; a document the scrubber blocked keeps its row (blocked = 1) and has no chunks.
CREATE TABLE source_cursor (
    source     TEXT NOT NULL,
    repo       TEXT NOT NULL,
    cursor     TEXT,
    last_sync  TEXT,
    last_error TEXT,
    PRIMARY KEY (source, repo)
) STRICT;

CREATE TABLE doc (
    repo    TEXT NOT NULL,
    path    TEXT NOT NULL,
    blob    TEXT NOT NULL,
    title   TEXT NOT NULL,
    blocked INTEGER NOT NULL CHECK (blocked IN (0, 1)),
    PRIMARY KEY (repo, path)
) STRICT;

CREATE TABLE doc_chunk (
    id         INTEGER PRIMARY KEY,
    repo       TEXT NOT NULL,
    path       TEXT NOT NULL,
    blob       TEXT NOT NULL,
    line_start INTEGER NOT NULL,
    line_end   INTEGER NOT NULL,
    text       TEXT NOT NULL,
    FOREIGN KEY (repo, path) REFERENCES doc (repo, path) ON DELETE CASCADE
) STRICT;

CREATE INDEX doc_chunk_by_doc ON doc_chunk (repo, path);

CREATE VIRTUAL TABLE doc_chunk_fts USING fts5 (text, content = 'doc_chunk', content_rowid = 'id', tokenize = 'unicode61');

CREATE TRIGGER doc_chunk_fts_insert AFTER INSERT ON doc_chunk BEGIN
    INSERT INTO doc_chunk_fts (rowid, text) VALUES (new.id, new.text);
END;

CREATE TRIGGER doc_chunk_fts_delete AFTER DELETE ON doc_chunk BEGIN
    INSERT INTO doc_chunk_fts (doc_chunk_fts, rowid, text) VALUES ('delete', old.id, old.text);
END;
