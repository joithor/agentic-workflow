-- API P12's recipe stats cache, the same shape as the other views (a key, the row as JSON text and the ledger sequence the stats were counted at). The key is
-- '<recipe>/<window>/<hash or ->'. The row is derived: the projector empties the table whenever a run starts or ends, and a rebuild leaves it empty, so a read recounts
-- from the ledger. Schema version 8 (ViewSchema.Version): a views.db written at version 7 gains this table here and the projector rebuilds, because the version differs.
CREATE TABLE view_recipe_stats (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
