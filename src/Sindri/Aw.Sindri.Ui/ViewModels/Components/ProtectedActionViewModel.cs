using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Components;

#pragma warning disable MA0182, CA1822, MA0041 // red stub: the U8 pages place the component
/// <summary>The lock button's popover (red stub).</summary>
internal sealed class ProtectedActionViewModel : ObservableObject
{
    public ProtectedActionViewModel(ProtectedAction action, IClipboard clipboard, TimeProvider clock)
    {
        _ = (action, clipboard, clock);
    }

    public string Label => throw new NotSupportedException();

    public string Title => throw new NotSupportedException();

    public string ButtonName => throw new NotSupportedException();

    public string Cli => throw new NotSupportedException();

    public bool CanCopy => throw new NotSupportedException();

    public bool IsOpen => throw new NotSupportedException();

    public string CopyLabel => throw new NotSupportedException();

    public string CopyName => throw new NotSupportedException();

    public bool IsAnnouncing => throw new NotSupportedException();

    public string Announcement => throw new NotSupportedException();

    public string OpenInAgentLabel => throw new NotSupportedException();

    public string OpenInAgentName => throw new NotSupportedException();

    public bool IsOpenInAgentPending => throw new NotSupportedException();

    public string OpenInAgentChip => throw new NotSupportedException();

    public void SetOpen(bool open) => throw new NotSupportedException();

    public Task Copy(CancellationToken cancellationToken) => throw new NotSupportedException();
}
#pragma warning restore CA1822, MA0041, MA0182
