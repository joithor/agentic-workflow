namespace Aw.Context.Core.Review;

/// <summary>Undecided entries per group.</summary>
/// <param name="Deterministic">Deterministic proposals.</param>
/// <param name="Model">Model proposals and suggested links.</param>
/// <param name="Orphaned">Orphaned overrides.</param>
public sealed record QueueCounts(int Deterministic, int Model, int Orphaned)
{
    /// <summary>Counts the entries of each group.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The counts.</returns>
    public static QueueCounts Of(IReadOnlyList<ReviewEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new QueueCounts(entries.Count(e => e.Group == ReviewGroup.Deterministic), entries.Count(e => e.Group == ReviewGroup.Model), entries.Count(e => e.Group == ReviewGroup.Orphaned));
    }
}
