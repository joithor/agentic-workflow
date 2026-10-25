namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One surface of a scope map, as words: the title, the small facts under it, the graph chip and a count.</summary>
/// <param name="Title">Title, else name, else id.</param>
/// <param name="Meta">Id, kind and lens, joined; empty when none.</param>
/// <param name="Detail">The detail sentence; empty when none.</param>
/// <param name="Graph">The graph status word as served; empty when none.</param>
/// <param name="Count">The files or citations count; empty when none.</param>
internal sealed record ScopeSurfaceRow(string Title, string Meta, string Detail, string Graph, string Count)
{
    /// <summary>The graph chip; empty when the surface has no graph status.</summary>
    public string Chip => Graph.Length > 0 ? Labels.ScopeGraphChip(Graph) : string.Empty;

    /// <summary>True when there is a chip.</summary>
    public bool HasChip => Graph.Length > 0;

    /// <summary>True when there is a count.</summary>
    public bool HasCount => Count.Length > 0;

    /// <summary>What assistive tech reads for the row.</summary>
    public string Name => Labels.ScopeRowName(Title, Chip, Count);

    /// <summary>True when there is a meta line.</summary>
    public bool HasMeta => Meta.Length > 0;

    /// <summary>True when there is a detail line.</summary>
    public bool HasDetail => Detail.Length > 0;
}
