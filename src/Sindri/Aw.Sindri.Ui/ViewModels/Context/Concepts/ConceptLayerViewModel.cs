namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The concept band: the first layer of the context map (red stub).</summary>
internal sealed class ConceptLayerViewModel : ObservableObject
{
    /// <summary>The concepts drawn, the concept of the unassigned nodes last.</summary>
    public IReadOnlyList<ConceptTileViewModel> Tiles { get; } = [];

    /// <summary>Whether the concepts are being read.</summary>
    public bool IsLoading { get; }

    /// <summary>The line that says why the map is shown without concepts; empty when there is none.</summary>
    public string Note { get; } = string.Empty;

    /// <summary>Whether the note has words.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Whether a search filters the concepts.</summary>
    internal bool SearchActive { get; }

    /// <summary>Whether the search kept no concept.</summary>
    internal bool NoMatches { get; }
}
