namespace Aw.Meta.Core.Watch;

/// <summary>The watch's memory between polls.</summary>
/// <param name="Initialized">Whether a first poll has set the baseline.</param>
/// <param name="Prs">Each pull request by number.</param>
/// <param name="MainSha">The last main commit.</param>
/// <param name="DiskLow">The last disk state reported.</param>
/// <param name="MemoryLow">The last memory state reported.</param>
/// <param name="DiskFlipSeen">Whether the previous poll already saw the disk state differ from the one reported (a flip needs two polls).</param>
/// <param name="MemoryFlipSeen">Whether the previous poll already saw the memory state differ from the one reported.</param>
public sealed record WatchState(bool Initialized, IReadOnlyDictionary<int, WatchedPr> Prs, string? MainSha, bool DiskLow, bool MemoryLow = false, bool DiskFlipSeen = false, bool MemoryFlipSeen = false)
{
    /// <summary>The state before any poll.</summary>
    public static WatchState Initial { get; } = new(Initialized: false, new Dictionary<int, WatchedPr>(), MainSha: null, DiskLow: false);

    /// <summary>The state before any poll, for a watch that continues one that already reported the given disk and memory states.</summary>
    /// <param name="resources">What the earlier watch last reported.</param>
    /// <returns>The state to start from.</returns>
    public static WatchState Continuing(WatchResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return Initial with { DiskLow = resources.DiskLow, MemoryLow = resources.MemoryLow };
    }
}
