using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>The map's deterministic layout (F3 spec 1 §2, "Layout"): a pure function of the lenses answer.</summary>
#pragma warning disable MA0182 // red stub: the page places it in the next commit
internal static class ContextMapLayout
{
    /// <summary>Places the nodes, bands and edges of an answer on the world.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <returns>The layout.</returns>
    internal static MapLayout Build(LensesView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
