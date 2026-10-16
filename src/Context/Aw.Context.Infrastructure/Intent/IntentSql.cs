namespace Aw.Context.Infrastructure.Intent;

/// <summary>The intent layer's SQL (context.db, migration 0006_s5b_intent_links). The cursor lives in source_cursor under repo '*'.</summary>
internal static class IntentSql
{
    internal const string CursorRepo = "*";

    /// <summary>A record that is new, or whose version moved, is queued for extraction again; the same version changes nothing (a replay writes no rows).</summary>
    internal const string UpsertRecord =
        "INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extracted_version, extraction_state) " +
        "VALUES (@key, @source, @version, @title, @aliases, @at, NULL, 'queued') " +
        "ON CONFLICT (key) DO UPDATE SET source = excluded.source, title = excluded.title, aliases = excluded.aliases, fetched_at = excluded.fetched_at, " +
        "extracted_version = NULL, extraction_state = 'queued', version = excluded.version WHERE intent_record.version <> excluded.version";

    internal const string DeleteLinks = "DELETE FROM intent_link WHERE record = @record";

    internal const string InsertLink =
        "INSERT INTO intent_link (record, target, rule, confidence, span_hash, synced_at) VALUES (@record, @target, @rule, @confidence, @span, @at) ON CONFLICT DO NOTHING";

    internal const string Synced =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, '*', @cursor, @at, NULL) " +
        "ON CONFLICT (source, repo) DO UPDATE SET cursor = COALESCE(excluded.cursor, source_cursor.cursor), last_sync = excluded.last_sync, last_error = NULL";

    internal const string Failed =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, '*', NULL, NULL, @code) " +
        "ON CONFLICT (source, repo) DO UPDATE SET last_error = excluded.last_error";

    internal const string Records = "SELECT key, source, version, title, aliases FROM intent_record ORDER BY key";

    internal const string Links = "SELECT record, target, rule, confidence, span_hash FROM intent_link ORDER BY record, target, rule";

    internal const string Decisions = "SELECT record, quote, wording, provenance, at FROM intent_decision ORDER BY key";

    internal const string Edges = "SELECT from_key, to_key, type, provenance, confidence, at FROM intent_edge ORDER BY from_key, to_key, type";

    internal const string State =
        "SELECT (SELECT cursor FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT last_sync FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT last_error FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT count(*) FROM intent_record WHERE source = @source), " +
        "(SELECT count(*) FROM intent_link l JOIN intent_record r ON r.key = l.record WHERE r.source = @source), " +
        "(SELECT count(*) FROM intent_decision d JOIN intent_record r ON r.key = d.record WHERE r.source = @source), " +
        "(SELECT count(*) FROM intent_record WHERE source = @source AND extraction_state = 'queued')";
}
