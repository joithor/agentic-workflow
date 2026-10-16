using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The pointer from a parked parent to the child it waits on: it opens the child by its id.</summary>
internal sealed class ChildPointerViewModel
{
    private readonly StatusChild _child;

    internal ChildPointerViewModel(StatusChild child)
    {
        _child = child;
        Text = Labels.ChildLine(child);
        Name = Labels.OpenInstanceName(child.Address);
    }

    /// <summary>Raised when the pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>The child's words.</summary>
    public string Text { get; }

    /// <summary>The control's accessible name: it carries the child's address.</summary>
    public string Name { get; }

    /// <summary>Follows the pointer.</summary>
    public void Open() => Opened?.Invoke(this, new FrameOpenEventArgs(_child.Address, _child.Id, fromPointer: true));
}
