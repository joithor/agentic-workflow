namespace Aw.Kernel.Data;

/// <summary>
/// The '<see cref="Outbox.OutboxTables.Commands"/>' table that <see cref="SqliteOutbox"/> reads and writes. A package that
/// uses the outbox adds <see cref="CreateMigration"/> to its migration set at the version it chooses; the SQL never
/// changes once shipped (an applied migration's checksum is checked), so a later change is a new migration.
/// </summary>
public static class SqliteOutboxSchema
{
    // The table and its pending index. Timestamps are UTC round-trip text (SqliteBind), so they sort as text; ids are
    // lowercase hyphenated Guid text. Written with explicit \n so the checksum never depends on the file's line endings.
    private const string CreateTable =
        "CREATE TABLE outbox_commands (\n"
        + "    id TEXT NOT NULL PRIMARY KEY,\n"
        + "    kind TEXT NOT NULL,\n"
        + "    payload_json TEXT NOT NULL,\n"
        + "    schema_version INTEGER NOT NULL,\n"
        + "    enqueued_at TEXT NOT NULL,\n"
        + "    actor TEXT NOT NULL,\n"
        + "    tenant TEXT NOT NULL,\n"
        + "    ring INTEGER NOT NULL,\n"
        + "    correlation_id TEXT NOT NULL,\n"
        + "    causation_id TEXT,\n"
        + "    trace_id TEXT,\n"
        + "    attempts INTEGER NOT NULL DEFAULT 0,\n"
        + "    last_error_code TEXT,\n"
        + "    last_error_message TEXT,\n"
        + "    next_attempt_at TEXT,\n"
        + "    dispatched_at TEXT,\n"
        + "    dead_lettered_at TEXT\n"
        + ");\n"
        + "CREATE INDEX outbox_commands_pending ON outbox_commands (enqueued_at)\n"
        + "    WHERE dispatched_at IS NULL AND dead_lettered_at IS NULL;\n";

    /// <summary>The migration that creates the table, at <paramref name="version"/> in a package's migration set.</summary>
    /// <param name="version">The version it takes in the package's set (1, 2, 3, no gaps).</param>
    /// <returns>The migration, named 'outbox_commands'.</returns>
    public static Migration CreateMigration(int version) => new(version, "outbox_commands", CreateTable);
}
