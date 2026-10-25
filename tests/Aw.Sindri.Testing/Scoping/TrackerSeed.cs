namespace Aw.Sindri.Testing.Scoping;

/// <summary>What a tracker holds in the contract's tests: the projects and issues both the in-memory source and the file one are built from.</summary>
/// <param name="Projects">The projects.</param>
/// <param name="Issues">The issues, in the tracker's order.</param>
public sealed record TrackerSeed(IReadOnlyList<TrackerSeedProject> Projects, IReadOnlyList<TrackerSeedIssue> Issues)
{
    /// <summary>The time every seeded record was last changed, which the record's version carries.</summary>
    public const string Updated = "2025-01-10T10:00:00.000Z";
}
