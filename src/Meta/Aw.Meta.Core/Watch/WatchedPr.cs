namespace Aw.Meta.Core.Watch;

/// <summary>What the watch has settled on for one pull request.</summary>
/// <param name="State">The reported state.</param>
/// <param name="IsDraft">The reported draft flag.</param>
/// <param name="Head">The reported head.</param>
/// <param name="Mergeable">The last reported mergeable value; null until first seen.</param>
/// <param name="Pending">A different mergeable value seen but not yet reported.</param>
/// <param name="PendingPolls">How many polls in a row saw the pending value.</param>
public sealed record WatchedPr(PrState State, bool IsDraft, string Head, string? Mergeable, string? Pending, int PendingPolls);
