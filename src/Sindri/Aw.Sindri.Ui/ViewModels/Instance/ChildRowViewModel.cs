#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823, IDE0032, CS0067 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One child instance of the children panel.</summary>
internal sealed class ChildRowViewModel : ObservableObject
{
    internal ChildRowViewModel(ChildRow row, IClipboard clipboard, TimeProvider clock) => throw new NotSupportedException();

    /// <summary>Raised when the row asks to open the child's frame.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>Raised when the row asks to open the child's item sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>The child's opaque id.</summary>
    public string Id => throw new NotSupportedException();

    /// <summary>The child's address.</summary>
    public string Address => throw new NotSupportedException();

    /// <summary>The child's item, or empty.</summary>
    public string ItemId => throw new NotSupportedException();

    /// <summary>The title as served.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The served chip.</summary>
    public StateChip Chip => throw new NotSupportedException();

    /// <summary>The mark of the child's progress.</summary>
    public string Glyph => throw new NotSupportedException();

    /// <summary>The state as served.</summary>
    public string State => throw new NotSupportedException();

    /// <summary>"r1/3", or empty.</summary>
    public string RoundText => throw new NotSupportedException();

    /// <summary>The agent as served, or empty.</summary>
    public string Agent => throw new NotSupportedException();

    /// <summary>"6m".</summary>
    public string Age => throw new NotSupportedException();

    /// <summary>The state, round, agent and age.</summary>
    public string Meta => throw new NotSupportedException();

    /// <summary>The park the engine mirrors here, in words; empty when none.</summary>
    public string MirroredText => throw new NotSupportedException();

    /// <summary>True when the child is done.</summary>
    public bool IsDone => throw new NotSupportedException();

    /// <summary>True when the child has an item to open.</summary>
    public bool CanOpenItem => throw new NotSupportedException();

    /// <summary>Open's accessible name.</summary>
    public string OpenName => throw new NotSupportedException();

    /// <summary>The chip that says the launch arrives with P10a; empty for a done child.</summary>
    public string LaunchChip => throw new NotSupportedException();

    /// <summary>The other actions the child serves.</summary>
    public IReadOnlyList<ChildActionViewModel> Actions => throw new NotSupportedException();

    /// <summary>True when there are other actions.</summary>
    public bool HasActions => throw new NotSupportedException();

    /// <summary>The title button's accessible name.</summary>
    public string OpenFrameName => throw new NotSupportedException();

    /// <summary>What the row draws, so a re-read of the same keeps it.</summary>
    internal string Signature => throw new NotSupportedException();

    /// <summary>Opens the child's frame by its id.</summary>
    public void OpenFrame() => throw new NotSupportedException();

    /// <summary>Opens the child's item sheet.</summary>
    public void OpenItem() => throw new NotSupportedException();
}
