namespace Aw.Context.Core.Review;

/// <summary>'context map review' (listing): what is left to decide in the latest draft.</summary>
/// <param name="Draft">The draft read ('v14@7c3e1a2').</param>
/// <param name="Version">The draft's number and commit.</param>
/// <param name="Entries">The undecided entries, each group cut at the call's limit: deterministic first, then model by impact, then orphaned.</param>
/// <param name="Decided">How many proposals of this draft have a standing decision.</param>
/// <param name="Proposals">Decided, deterministic and model proposals together (orphaned overrides are not proposals).</param>
/// <param name="Counts">Undecided entries per group, before the cut.</param>
/// <param name="ByKind">The undecided deterministic node proposals per node kind.</param>
/// <param name="Truncated">Which groups were cut.</param>
public sealed record ReviewQueueView(
    string Draft,
    ReviewVersion Version,
    IReadOnlyList<ReviewEntry> Entries,
    int Decided,
    int Proposals,
    QueueCounts Counts,
    IReadOnlyDictionary<string, int> ByKind,
    ReviewTruncated Truncated)
{
    /// <summary>How many entries are undecided, before any cut; 'map promote' refuses while it is above zero (SND-CONTEXT-046).</summary>
    public int Undecided => Counts.Deterministic + Counts.Model + Counts.Orphaned;

    /// <summary>A queue whose entries are all it has: counts from the entries, nothing cut, no version.</summary>
    /// <param name="draft">The draft read.</param>
    /// <param name="entries">The undecided entries.</param>
    /// <param name="decided">How many proposals already have a decision.</param>
    /// <returns>The queue.</returns>
    public static ReviewQueueView Of(string draft, IReadOnlyList<ReviewEntry> entries, int decided)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var counts = QueueCounts.Of(entries);
        return new ReviewQueueView(draft, ReviewVersion.None, entries, decided, decided + counts.Deterministic + counts.Model, counts, new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);
    }

    /// <summary>The same queue with each group cut to <paramref name="limit"/> entries; the counts stay full and <see cref="Truncated"/> says which group lost entries.</summary>
    /// <param name="limit">The most entries per group.</param>
    /// <returns>The cut queue.</returns>
    public ReviewQueueView Capped(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        List<ReviewEntry> kept = [];
        var truncated = new bool[3];
        foreach (var group in new[] { ReviewGroup.Deterministic, ReviewGroup.Model, ReviewGroup.Orphaned })
        {
            var inGroup = Entries.Where(e => e.Group == group).ToList();
            kept.AddRange(inGroup.Take(limit));
            truncated[(int)group] = inGroup.Count > limit;
        }

        return this with { Entries = kept, Truncated = new ReviewTruncated(truncated[0], truncated[1], truncated[2]) };
    }
}
