using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>An item fact: the item's id and what the engine's summary says after it (its state), and Open, which opens the item's sheet.</summary>
internal sealed class ItemFactViewer : ObservableObject, IFactViewer
{
    private readonly FactViewerContext _context;

    internal ItemFactViewer(FactRow row, FactViewerContext context)
    {
        (Row, _context) = (row, context);
        ItemId = row.Ref?.ItemId ?? string.Empty;
        var summary = FactText.Scrub(row.Summary);
        Detail = ItemId.Length > 0 && summary.StartsWith(ItemId, StringComparison.Ordinal) ? summary[ItemId.Length..].Trim() : summary;
        OpenName = Labels.OpenItemName(ItemId.Length > 0 ? ItemId : row.Label);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The item's id.</summary>
    public string ItemId { get; }

    /// <summary>What follows the id in the served summary (its state).</summary>
    public string Detail { get; }

    /// <summary>True when the fact names an item.</summary>
    public bool CanOpen => ItemId.Length > 0;

    /// <summary>Open's accessible name.</summary>
    public string OpenName { get; }

    /// <summary>An item viewer needs nothing beyond the row.</summary>
    /// <param name="cancellationToken">Cancels nothing.</param>
    /// <returns>A completed task.</returns>
    public Task Load(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Opens the item's sheet.</summary>
    public void Open()
    {
        if (CanOpen)
        {
            _context.OpenItem(ItemId);
        }
    }
}
