-- The code index view (code index spec §8): the newest 'codeindex.built' per repo, keyed by the repo name, the row as JSON text and the
-- ledger sequence that last wrote it. Schema version 8 (ViewSchema.Version): a views.db written at version 7 gains this table here and the
-- projector rebuilds it from the ledger, because the version differs.
CREATE TABLE view_code_index (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
