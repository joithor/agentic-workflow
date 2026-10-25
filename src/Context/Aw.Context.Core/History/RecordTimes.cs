namespace Aw.Context.Core.History;

/// <summary>The two instants of a stored record: when the source created it and when it last changed (its version, when that is a timestamp).</summary>
/// <param name="CreatedAt">Creation, or null.</param>
/// <param name="UpdatedAt">Last change, or null.</param>
public sealed record RecordTimes(DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt)
{
    /// <summary>Reads a stored record's times.</summary>
    /// <param name="createdAt">The stored creation instant, or null.</param>
    /// <param name="version">The source's version string.</param>
    /// <returns>The times; <see cref="UpdatedAt"/> is null when the version isn't a timestamp.</returns>
    public static RecordTimes Of(DateTimeOffset? createdAt, string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new RecordTimes(createdAt, HistoryWindow.TryInstant(version, out var updated) ? updated : null);
    }
}
