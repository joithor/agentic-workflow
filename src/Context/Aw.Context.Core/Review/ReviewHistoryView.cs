namespace Aw.Context.Core.Review;

/// <summary>The override log, newest first: what "map review --history" lists so a person can undo one decision.</summary>
/// <param name="Entries">The overrides, newest first.</param>
public sealed record ReviewHistoryView(IReadOnlyList<ReviewHistoryEntry> Entries);
