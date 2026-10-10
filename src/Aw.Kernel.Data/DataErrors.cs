namespace Aw.Kernel.Data;

/// <summary>The data layer's error codes (AW-DATA-…). Expected store failures become these; anything else is a bug and throws.</summary>
public static class DataErrors
{
    /// <summary>AW-DATA-001: a query that must return one row returned none.</summary>
    public static readonly ErrorDefinition NotFound = new("AW-DATA-001", ErrorKind.NotFound, "Row not found");

    /// <summary>AW-DATA-002: the database file can't be opened, read or written (missing directory, read-only, full, not a database).</summary>
    public static readonly ErrorDefinition Unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "Database unavailable");

    /// <summary>AW-DATA-003: another connection held the lock for longer than the busy timeout.</summary>
    public static readonly ErrorDefinition Busy = new("AW-DATA-003", ErrorKind.Timeout, "Database busy");

    /// <summary>AW-DATA-004: a write broke a constraint (unique, foreign key, not null, check).</summary>
    public static readonly ErrorDefinition Constraint = new("AW-DATA-004", ErrorKind.Conflict, "Constraint violated");

    /// <summary>AW-DATA-005: the migration set is malformed (a gap, a duplicate, out of order, or a file without a version).</summary>
    public static readonly ErrorDefinition MigrationSequence = new("AW-DATA-005", ErrorKind.Invalid, "Migration set malformed");

    /// <summary>
    /// AW-DATA-006: the database's applied history doesn't match the build: an applied migration's text or name
    /// changed, or schema_version isn't exactly 1, 2, …, n (a gap, version 0 or less).
    /// </summary>
    public static readonly ErrorDefinition MigrationDrift = new("AW-DATA-006", ErrorKind.Conflict, "Applied migration history changed");

    /// <summary>AW-DATA-007: the database has a migration this build doesn't know (it is newer than the build).</summary>
    public static readonly ErrorDefinition MigrationUnknown = new("AW-DATA-007", ErrorKind.Conflict, "Database newer than this build");

    /// <summary>
    /// AW-DATA-008: the file is damaged or isn't a SQLite database (SQLITE_CORRUPT, SQLITE_NOTADB). Not retryable:
    /// stop and restore from a backup.
    /// </summary>
    public static readonly ErrorDefinition Damaged = new("AW-DATA-008", ErrorKind.Invalid, "Database damaged");

    /// <summary>
    /// AW-DATA-009: the database has tables but no schema_version table, or a schema_version with other columns than
    /// this runner's, so this runner didn't create it (usually a path pointing at the wrong file). Refused before
    /// anything is written.
    /// </summary>
    public static readonly ErrorDefinition ForeignDatabase = new("AW-DATA-009", ErrorKind.Conflict, "Database not managed by this runner");

    /// <summary>Every data code, in code order.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [NotFound, Unavailable, Busy, Constraint, MigrationSequence, MigrationDrift, MigrationUnknown, Damaged, ForeignDatabase];
}
