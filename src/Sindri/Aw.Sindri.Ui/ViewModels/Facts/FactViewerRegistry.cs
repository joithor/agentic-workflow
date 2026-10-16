#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>Which viewer shows which fact type. A type with no viewer shows as a payload.</summary>
internal sealed class FactViewerRegistry
{
    /// <summary>The registry with the five viewers U8 ships.</summary>
    /// <returns>A new registry.</returns>
    internal static FactViewerRegistry CreateDefault() => throw new NotSupportedException();

    /// <summary>Registers a viewer for a type, replacing any.</summary>
    /// <param name="type">The type.</param>
    /// <param name="factory">Makes the viewer for a row.</param>
    internal void Register(FactType type, Func<FactRow, FactViewerContext, IFactViewer> factory) => throw new NotSupportedException();

    /// <summary>Whether a type has a viewer of its own.</summary>
    /// <param name="type">The type.</param>
    /// <returns>True when registered.</returns>
    internal bool IsRegistered(FactType type) => throw new NotSupportedException();

    /// <summary>Makes the viewer for a row: its type's, else the payload viewer.</summary>
    /// <param name="row">The row.</param>
    /// <param name="context">The frame's context.</param>
    /// <returns>The viewer.</returns>
    internal IFactViewer Create(FactRow row, FactViewerContext context) => throw new NotSupportedException();
}
