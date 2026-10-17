namespace Aw.Context.Core.History;

/// <summary>The pure rules of the history lens: the 'before' and 'since' cut, the union across anchors, the order and the caps (S-H decisions 3, 5, 6).</summary>
public static class HistoryCut
{
    /// <summary>Cuts and orders records and decisions. The same item reached by two anchors becomes one entry listing both.</summary>
    /// <param name="records">Records, one per (record, anchor) or already unioned.</param>
    /// <param name="decisions">Decisions, likewise.</param>
    /// <param name="window">The window.</param>
    /// <returns>The kept lists and counts.</returns>
    public static HistoryCutResult Apply(IReadOnlyList<HistoryRecord> records, IReadOnlyList<HistoryDecision> decisions, HistoryWindow window) => throw new NotSupportedException();

    /// <summary>Unions changes by sha and repo, orders them and caps the list.</summary>
    /// <param name="changes">The changes, possibly one per anchor.</param>
    /// <returns>The list.</returns>
    public static HistoryChangeList Changes(IReadOnlyList<HistoryChange> changes) => throw new NotSupportedException();
}
