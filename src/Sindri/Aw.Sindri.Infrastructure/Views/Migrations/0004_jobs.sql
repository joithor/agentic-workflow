-- API P6's jobs view, the same shape as 0001's (a key, the row as JSON text and the ledger sequence that last wrote it) plus the columns
-- the job queries look rows up by, each a virtual column generated from the row's JSON and indexed.
-- Schema version 4 (ViewSchema.Version): a views.db written at version 3 gains this table here and the projector rebuilds it from the
-- ledger, because the version differs. The rebuild also refolds the needs-you rows, whose failure row's cli is now 'sindri services run <name>'.
CREATE TABLE view_jobs (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL,
    kind TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.kind')) VIRTUAL,
    repo TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.repo')) VIRTUAL
);
CREATE INDEX view_jobs_state ON view_jobs (state);
CREATE INDEX view_jobs_kind ON view_jobs (kind);
CREATE INDEX view_jobs_repo ON view_jobs (repo);
