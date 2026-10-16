using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The pointer from a parked parent to the child it waits on: it opens the child by its id.</summary>
internal sealed class ChildPointerViewModel : ObservableObject
{
    private StatusChild _child;

    internal ChildPointerViewModel(StatusChild child)
    {
        _child = child;
        (Text, Name) = (string.Empty, string.Empty);
        Update(child);
    }

    /// <summary>Takes the child as served now (its state moves the words, not the control).</summary>
    /// <param name="child">The served child.</param>
    internal void Update(StatusChild child)
    {
        _child = child;
        Text = Labels.ChildLine(child);
        Name = Labels.OpenInstanceName(Text, child.Address);
    }

    /// <summary>Raised when the pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>The child's words.</summary>
    public string Text { get; private set => Set(ref field, value); }

    /// <summary>The control's accessible name: it leads with the drawn words and carries the child's address.</summary>
    public string Name { get; private set => Set(ref field, value); }

    /// <summary>Follows the pointer.</summary>
    public void Open() => Opened?.Invoke(this, new FrameOpenEventArgs(_child.Address, _child.Id, fromPointer: true));
}
