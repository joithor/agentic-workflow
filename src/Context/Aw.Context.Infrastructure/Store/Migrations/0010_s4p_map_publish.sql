-- Stage F1P: publishing a refreshed map together with the code index (retrieval spec §8.7a, code index spec §6.4).
-- map_version_head: the commit each mapped repo was at when a version was built, kept with the version (cascade-deleted with it,
-- so retention removes it; a retained version keeps it for as-of reads). map_hold: the one draft that waits for the code index; it
-- goes with that draft. map_publish_chain: the start of the wait, one row, tied to no draft so it survives a rebuild; a promote clears both.
CREATE TABLE map_version_head (
    version     INTEGER NOT NULL REFERENCES map_version (id) ON DELETE CASCADE,
    repo        TEXT    NOT NULL,
    commit_sha  TEXT    NOT NULL CHECK (length(commit_sha) BETWEEN 7 AND 40 AND commit_sha NOT GLOB '*[^0-9a-f]*'),
    on_default  INTEGER NOT NULL CHECK (on_default IN (0, 1)),
    PRIMARY KEY (version, repo)
) STRICT;

CREATE TABLE map_hold (
    version  INTEGER PRIMARY KEY REFERENCES map_version (id) ON DELETE CASCADE
) STRICT;

CREATE TABLE map_publish_chain (
    id     INTEGER PRIMARY KEY CHECK (id = 1),
    since  TEXT    NOT NULL
) STRICT;
