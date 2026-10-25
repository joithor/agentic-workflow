using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The concept band (<see cref="ContextPageViewModel"/>, continued; red stub).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The first layer of the map: the product's concepts.</summary>
    public ConceptLayerViewModel Concepts { get; } = new();

    /// <summary>Whether the concept layer is on the stage.</summary>
    public bool ShowConcepts => false;

    /// <summary>The id of the concept opened; null on the concept layer and when the map has no concepts.</summary>
    internal string? OpenedConceptId => null;

    /// <summary>Opens a concept: the three bands, scoped to it.</summary>
    /// <param name="id">The concept's id ("other" for the unassigned nodes).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scoped map is drawn.</returns>
    internal Task OpenConcept(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>Goes up one layer: a node's panel, a query, an open feature, then the concept.</summary>
    /// <returns>True when it went up.</returns>
    internal bool Up() => throw new NotSupportedException();
}
