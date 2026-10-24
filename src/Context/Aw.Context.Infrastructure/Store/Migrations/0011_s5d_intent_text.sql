-- Stage S-5d: the cleaned (scrubbed, never raw) text of an intent record, kept so a new current map can link the stored records again without fetching them (the linker reads text, and the map moved).
-- Nullable: a row stored before this migration has none until a sync fetches the record again; a record without text keeps the links it has. Written by the sync with every fetch of the record.
ALTER TABLE intent_record ADD COLUMN text TEXT;
