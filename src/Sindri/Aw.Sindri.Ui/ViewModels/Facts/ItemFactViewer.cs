#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>An item fact: the item's id and what the engine said of it, and Open, which opens the item's sheet.</summary>
internal sealed class ItemFactViewer : ObservableObject, IFactViewer
{
    internal ItemFactViewer(FactRow row, FactViewerContext context) => throw new NotSupportedException();

    public FactRow Row => throw new NotSupportedException();

    /// <summary>The item's id.</summary>
    public string ItemId => throw new NotSupportedException();

    /// <summary>What follows the id in the served summary (its state).</summary>
    public string Detail => throw new NotSupportedException();

    /// <summary>Open's accessible name.</summary>
    public string OpenName => throw new NotSupportedException();

    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Opens the item's sheet.</summary>
    public void Open() => throw new NotSupportedException();
}
