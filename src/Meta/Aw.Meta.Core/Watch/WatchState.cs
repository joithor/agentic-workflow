namespace Aw.Meta.Core.Watch;

/// <summary>The watch's memory between polls.</summary>
/// <param name="Initialized">Whether a first poll has set the baseline.</param>
/// <param name="Prs">Each pull request by number.</param>
/// <param name="MainSha">The last main commit.</param>
/// <param name="DiskLow">The last disk state reported.</param>
/// <param name="MemoryLow">The last memory state reported.</param>
public sealed record WatchState(bool Initialized, IReadOnlyDictionary<int, WatchedPr> Prs, string? MainSha, bool DiskLow, bool MemoryLow = false)
{
    /// <summary>The state before any poll.</summary>
    public static WatchState Initial { get; } = new(Initialized: false, new Dictionary<int, WatchedPr>(), MainSha: null, DiskLow: false);
}
