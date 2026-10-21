#pragma warning disable MA0182 // until slice 3 places the run page

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Places the run canvas's cards (F3 spec 3 §2, "Deterministic layout"). Pure: the heights come in measured, nothing is measured here.</summary>
internal static class RunCanvasLayout
{
    /// <summary>The place of every card of a graph.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="heights">The measured height of each card by id; a card not in it gets a default.</param>
    /// <returns>The layout.</returns>
    internal static RunLayout Of(RunGraph graph, IReadOnlyDictionary<string, double> heights) => throw new NotSupportedException();
}

#pragma warning restore MA0182
