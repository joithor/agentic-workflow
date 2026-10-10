namespace Aw.Kernel.Data;

/// <summary>Where the database file is and how long a writer waits for another writer's lock.</summary>
public sealed record SqliteOptions
{
    /// <summary>Creates options for the database at <paramref name="databasePath"/>.</summary>
    /// <param name="databasePath">The database file. Its directory must exist.</param>
    public SqliteOptions(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
    }

    /// <summary>The database file.</summary>
    public string DatabasePath { get; }

    /// <summary>How long to wait for a lock held by another connection before failing with AW-DATA-003. Default 5 s.</summary>
    public TimeSpan BusyTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromSeconds(5);
}
