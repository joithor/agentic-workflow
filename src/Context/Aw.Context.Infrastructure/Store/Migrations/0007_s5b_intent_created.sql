-- Stage S-H (named for the intent layer it extends, the loader wants a numbered stage): when the source created an intent record, so a 'before' read of the history lens can drop records made after the cut.
-- Nullable: a row stored before this migration, or from a source that gives no creation time, reads null, and a 'before' read labels it leaky.
-- A row's value is written when its version moves (the upsert rewrites a row only then), so older rows fill in as their records change.
ALTER TABLE intent_record ADD COLUMN created_at TEXT;
