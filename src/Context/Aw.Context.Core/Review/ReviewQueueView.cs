namespace Aw.Context.Core.Review;

/// <summary>'context map review' (listing): what is left to decide in the latest draft.</summary>
/// <param name="Draft">The draft read ('v14@7c3e1a2').</param>
/// <param name="Entries">The undecided entries: deterministic first, then model by impact, then orphaned.</param>
/// <param name="Decided">How many proposals already have a decision.</param>
public sealed record ReviewQueueView(string Draft, IReadOnlyList<ReviewEntry> Entries, int Decided)
{
    /// <summary>How many entries are undecided; 'map promote' refuses while it is above zero (SND-CONTEXT-046).</summary>
    public int Undecided => Entries.Count;
}
