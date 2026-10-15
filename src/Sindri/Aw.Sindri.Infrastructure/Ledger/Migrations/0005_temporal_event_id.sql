-- The Temporal event id of a mirrored row (engine spec §5.6, UI E-30). 'RecordEvent' writes the id of the Temporal activity
-- it ran as, the third part of its idempotency key 'workflow:{workflow id}:{run id}:{activity id}', so the column and the key
-- never disagree. Null on every row the engine didn't mirror. The instance is the payload's 'instance' member, so the history
-- API joins a page of event ids to ledger sequences with one query on (instance, temporal_event_id). The index holds the
-- mirrored rows only (a row without the id has no payload to read the instance from).
ALTER TABLE events ADD COLUMN temporal_event_id TEXT;
CREATE INDEX events_instance_temporal ON events (json_extract(payload_json, '$.instance'), temporal_event_id) WHERE temporal_event_id IS NOT NULL;
