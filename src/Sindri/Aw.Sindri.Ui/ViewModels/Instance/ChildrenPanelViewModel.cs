#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823, IDE0032, CS0067 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The children panel (screens §4.2): the child instances, grouped under the state that spawned them and, for a packet, by workstream.</summary>
internal sealed class ChildrenPanelViewModel : ObservableObject
{
    internal ChildrenPanelViewModel(InstanceDetail detail, IClipboard clipboard, TimeProvider clock) => throw new NotSupportedException();

    /// <summary>Raised when a row asks to open its child's frame.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>Raised when a row asks to open its item's sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>Raised when a group asks to list every child in Work.</summary>
    internal event EventHandler? WorkRequested;

    /// <summary>The groups, in the order served.</summary>
    public IReadOnlyList<ChildGroupViewModel> Groups => throw new NotSupportedException();

    /// <summary>True when the recipe has children.</summary>
    public bool HasGroups => throw new NotSupportedException();

    /// <summary>"CHILDREN 9".</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>The words when there are no children.</summary>
    public string EmptyText => throw new NotSupportedException();

    /// <summary>The panel's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>What the panel draws, so a re-read of the same keeps it.</summary>
    internal string Signature => throw new NotSupportedException();
}
