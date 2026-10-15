namespace Aw.Sindri.Ui.ViewModels.Components;

#pragma warning disable MA0182 // a stub until the U8 pages place the component
/// <summary>The Attach component's view model (red stub).</summary>
internal sealed class AttachControlViewModel(AttachInput input) : ObservableObject
{
    private AttachInput _input = input;

    public event EventHandler<AttachChosenEventArgs>? Chosen
    {
        add => throw Red();
        remove => throw Red();
    }

    public string PrimaryLabel => throw Red();

    public string PrimaryName => throw Red();

    public AttachAction PrimaryAction => throw Red();

    public bool IsPrimaryShown => throw Red();

    public bool IsPending => throw Red();

    public string PendingChip => throw Red();

    public bool HasNote => throw Red();

    public string Note => throw Red();

    public string ChevronName => throw Red();

    public IReadOnlyList<AttachMenuItem> Menu => throw Red();

    public bool IsConfirmingCancel => throw Red();

    public string ConfirmLine => throw Red();

    public string ConfirmButtonName => throw Red();

    public string KeepButtonName => throw Red();

    public bool IsConfirmLive => throw Red();

    public string ConfirmAnnouncement => throw Red();

    public void Update(AttachInput input) => _input = input;

    public void Choose(AttachAction action) => throw Red(action.ToString());

    public void ConfirmCancel() => throw Red();

    public void Keep() => throw Red();

    private NotSupportedException Red(string? what = null) => new("red: " + _input.Address + what);
}
#pragma warning restore MA0182
