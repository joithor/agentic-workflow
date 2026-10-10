namespace Aw.Kernel.Data;

/// <summary>What a migration run did.</summary>
/// <param name="FromVersion">The schema version before the run.</param>
/// <param name="ToVersion">The schema version after it.</param>
/// <param name="Applied">The versions this run applied, in order.</param>
public sealed record MigrationReport(int FromVersion, int ToVersion, IReadOnlyList<int> Applied)
{
    /// <inheritdoc />
    public bool Equals(MigrationReport? other) =>
        other is not null && FromVersion == other.FromVersion && ToVersion == other.ToVersion && Applied.SequenceEqual(other.Applied);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FromVersion, ToVersion, Applied.Count);
}
