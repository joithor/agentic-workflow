#pragma warning disable CA1822, MA0041, IDE0060, CS0067 // red stub: replaced by the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's facts panel (<see cref="InstanceFrameViewModel"/>, continued; U8 T5).</summary>
internal sealed partial class InstanceFrameViewModel
{
    /// <summary>Raised when a panel asks to open an item's sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>Raised when the children panel asks to list every child in Work.</summary>
    internal event EventHandler? WorkRequested;

    /// <summary>Which viewer shows which fact type. A later phase registers its own here at start-up, in its own files.</summary>
    internal FactViewerRegistry Viewers { get; } = viewers ?? FactViewerRegistry.CreateDefault();

    /// <summary>The facts panel; null before a read.</summary>
    public FactsPanelViewModel? Facts { get; private set => Set(ref field, value); }

    /// <summary>The children panel; null before a read.</summary>
    public ChildrenPanelViewModel? Children { get; private set => Set(ref field, value); }

    // A re-read serves the same facts again: the panel stays (and its open viewers with it) unless a row changed, and then the open ones stay open.
    private void ShowPanels(InstanceDetail detail)
    {
        var context = new FactViewerContext(api, clipboard, clock, () => _url, detail.Id, id => ItemRequested?.Invoke(this, new ItemOpenEventArgs(id)));
        var fresh = new FactsPanelViewModel(detail, Viewers, context);
        if (Facts is null || !string.Equals(Facts.Signature, fresh.Signature, StringComparison.Ordinal))
        {
            Facts = Facts is { OpenKeys.Count: > 0 } shown ? new FactsPanelViewModel(detail, Viewers, context, shown.OpenKeys) : fresh;
        }

        var children = new ChildrenPanelViewModel(detail, clipboard, clock);
        if (Children is null || !string.Equals(Children.Signature, children.Signature, StringComparison.Ordinal))
        {
            children.Opened += (_, e) => OpenRequested?.Invoke(this, e);
            children.ItemRequested += (_, e) => ItemRequested?.Invoke(this, e);
            children.WorkRequested += (_, _) => WorkRequested?.Invoke(this, EventArgs.Empty);
            Children = children;
        }
    }

    /// <summary>Raised when the frame asks the view to put focus on one of its parts.</summary>
    internal event EventHandler<FrameFocusEventArgs>? FocusRequested;

    /// <summary>The tab strip; null before a read.</summary>
    public FrameTabsViewModel? Tabs { get; private set => Set(ref field, value); }

    /// <summary>Names what the key that opened this frame wants done once the instance is read.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="intent">The intent.</param>
    internal void Intend(string address, FrameIntent intent)
    {
    }

    private void ClearPanels() => (Facts, Children) = (null, null);
}
