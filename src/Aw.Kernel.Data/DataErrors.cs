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

    /// <summary>AW-DATA-006: a migration the database already applied has different text now.</summary>
    public static readonly ErrorDefinition MigrationDrift = new("AW-DATA-006", ErrorKind.Conflict, "Applied migration changed");

    /// <summary>AW-DATA-007: the database has a migration this build doesn't know (it is newer than the build).</summary>
    public static readonly ErrorDefinition MigrationUnknown = new("AW-DATA-007", ErrorKind.Conflict, "Database newer than this build");

    /// <summary>Every data code, in code order.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [NotFound, Unavailable, Busy, Constraint, MigrationSequence, MigrationDrift, MigrationUnknown];
}
