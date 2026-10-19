namespace Aw.Context.Core.History;

/// <summary>The merged, ordered and capped changes.</summary>
/// <param name="Items">Newest first.</param>
/// <param name="Total">Changes before the cap.</param>
public sealed record HistoryChangeList(IReadOnlyList<HistoryChange> Items, int Total)
{
    /// <summary>True when the list was cut at the cap.</summary>
    public bool Truncated => Total > Items.Count;
}
