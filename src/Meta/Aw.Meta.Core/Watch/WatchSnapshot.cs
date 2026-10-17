namespace Aw.Meta.Core.Watch;

/// <summary>Everything one poll saw.</summary>
/// <param name="Prs">The pull requests.</param>
/// <param name="MainSha">The main commit.</param>
/// <param name="DiskLow">Whether free disk is under the floor.</param>
/// <param name="MemoryLow">Whether available memory is under its floor or swap is nearly all in use.</param>
public sealed record WatchSnapshot(IReadOnlyList<PrView> Prs, string MainSha, bool DiskLow, bool MemoryLow = false);
