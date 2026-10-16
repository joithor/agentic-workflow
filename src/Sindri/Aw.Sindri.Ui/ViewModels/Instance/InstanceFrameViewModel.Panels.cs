using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's facts panel (<see cref="InstanceFrameViewModel"/>, continued; U8 T5).</summary>
internal sealed partial class InstanceFrameViewModel
{
    /// <summary>Raised when a panel asks to open an item's sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>Which viewer shows which fact type. A later phase registers its own here at start-up, in its own files.</summary>
    internal FactViewerRegistry Viewers { get; } = viewers ?? FactViewerRegistry.CreateDefault();

    /// <summary>The facts panel; null before a read.</summary>
    public FactsPanelViewModel? Facts { get; private set => Set(ref field, value); }

    /// <summary>The children panel; null before a read.</summary>
#pragma warning disable CA1822, MA0041 // red stub
    public ChildrenPanelViewModel? Children => throw new NotSupportedException();
#pragma warning restore CA1822, MA0041

    // A re-read serves the same facts again: the panel stays (and its open viewers with it) unless a row changed, and then the open ones stay open.
    private void ShowPanels(InstanceDetail detail)
    {
        var context = new FactViewerContext(api, clipboard, clock, () => _url, detail.Id, id => ItemRequested?.Invoke(this, new ItemOpenEventArgs(id)));
        var fresh = new FactsPanelViewModel(detail, Viewers, context);
        if (Facts is null || !string.Equals(Facts.Signature, fresh.Signature, StringComparison.Ordinal))
        {
            Facts = Facts is { OpenKeys.Count: > 0 } shown ? new FactsPanelViewModel(detail, Viewers, context, shown.OpenKeys) : fresh;
        }
    }

    private void ClearPanels() => Facts = null;
}
