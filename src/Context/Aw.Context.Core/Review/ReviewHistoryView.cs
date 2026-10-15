namespace Aw.Context.Core.Review;

/// <summary>The override log, newest first: what "map review --history" lists so a person can undo one decision.</summary>
/// <param name="Entries">The newest overrides, at most the call's limit.</param>
/// <param name="Truncated">True when older ones were cut.</param>
public sealed record ReviewHistoryView(IReadOnlyList<ReviewHistoryEntry> Entries, bool Truncated);
