namespace Aw.Context.Core.Review;

/// <summary>What <see cref="ReviewFileMatcher"/> made of a review file.</summary>
/// <param name="Decisions">The decisions for entries that still sit in the queue.</param>
/// <param name="Skipped">Decided entries that match nothing in the latest draft, or whose verb a file can't carry.</param>
public sealed record ReviewFileMatch(IReadOnlyList<ReviewDecision> Decisions, IReadOnlyList<string> Skipped);
