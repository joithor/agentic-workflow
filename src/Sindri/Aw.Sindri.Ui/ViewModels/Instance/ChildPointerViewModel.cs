#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The pointer from a parked parent to the child it waits on.</summary>
internal sealed class ChildPointerViewModel
{
    internal ChildPointerViewModel(StatusChild child) => throw new NotSupportedException();

    /// <summary>Raised when the pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>The child's words.</summary>
    public string Text => throw new NotSupportedException();

    /// <summary>The control's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>Follows the pointer.</summary>
    public void Open() => Opened?.Invoke(this, new FrameOpenEventArgs(string.Empty, null));
}
