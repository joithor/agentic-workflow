namespace Aw.Sindri.Ui.ViewModels.Components;

#pragma warning disable MA0182 // until the U8 pages place the component
#pragma warning disable CA1822, MA0041 // the two fixed sentences are properties because the view binds them
/// <summary>
/// The one Attach component (screens spec §4.5, rev 8): the main action by Step kind, a chevron with the rest, and a
/// Cancel that asks first. A live agent Step gets Attach, with Take over, File a request and Cancel in the chevron; a
/// tool or model Step has no terminal, so it gets Open instance, with Take over and Cancel. Until the command catalog
/// says Attach is ready (API P10a) the main action is a dashed chip, never a live-looking button. The component asks
/// (<see cref="Chosen"/>); it sends nothing itself. Addresses are opaque strings and never parsed.
/// </summary>
internal sealed class AttachControlViewModel : ObservableObject
{
    private AttachInput _input;

    public AttachControlViewModel(AttachInput input)
    {
        _input = input;
        Menu = MenuFor(input);
    }

    /// <summary>Raised when an action is chosen and no confirmation stands in its way.</summary>
    public event EventHandler<AttachChosenEventArgs>? Chosen;

    /// <summary>The main button's label.</summary>
    public string PrimaryLabel => IsAgent ? Labels.Attach : Labels.OpenInstance;

    /// <summary>The main button's accessible name, which says what it acts on.</summary>
    public string PrimaryName => IsAgent ? Labels.AttachTo(_input.Address) : Labels.OpenInstanceName(_input.Address);

    /// <summary>What the main button asks for.</summary>
    public AttachAction PrimaryAction => IsAgent ? AttachAction.Attach : AttachAction.OpenInstance;

    /// <summary>True when the main button shows (it does not while Attach is pending).</summary>
    public bool IsPrimaryShown => !IsPending;

    /// <summary>True for a live agent Step whose Attach the catalog has not made ready.</summary>
    public bool IsPending => IsAgent && !_input.AttachReady;

    /// <summary>The chip that stands in for Attach while it is pending.</summary>
    public string PendingChip => _input.PendingReason ?? Labels.AvailableWithP10a;

    /// <summary>True when the Attach note shows: under a live Attach button, not under a chip or Open instance.</summary>
    public bool HasNote => IsAgent && _input.AttachReady;

    /// <summary>The note under every Attach button.</summary>
    public string Note => Labels.AttachNote;

    /// <summary>The chevron's accessible name.</summary>
    public string ChevronName => Labels.MoreActionsFor(_input.Address);

    /// <summary>The chevron's entries.</summary>
    public IReadOnlyList<AttachMenuItem> Menu { get; private set => Set(ref field, value); }

    /// <summary>True while the one-line Cancel confirm shows.</summary>
    public bool IsConfirmingCancel { get; private set => Set(ref field, value); }

    /// <summary>The confirm line: the served sentence when there is one, else "Cancel &lt;address&gt;?".</summary>
    public string ConfirmLine => _input.ConfirmText is { Length: > 0 } served ? served : Labels.CancelAsk(_input.Address);

    /// <summary>The go-ahead button's accessible name.</summary>
    public string ConfirmButtonName => Labels.ConfirmCancelName(_input.Address);

    /// <summary>The go-ahead button's visible label; its name starts with it.</summary>
    public string ConfirmLabel => Labels.ConfirmCancel;

    /// <summary>The Keep button's visible label; its name starts with it.</summary>
    public string KeepLabel => Labels.KeepStep;

    /// <summary>The pending chip's accessible name: the main action it stands for and what it waits for.</summary>
    public string PendingChipName => Labels.AttachPendingName(_input.Address, PendingChip);

    /// <summary>The Keep button's accessible name.</summary>
    public string KeepButtonName => Labels.KeepName(_input.Address);

    /// <summary>The confirm's live region flag: on while the confirm shows. It changes before the text does.</summary>
    public bool IsConfirmLive { get; private set => Set(ref field, value); }

    /// <summary>The confirm's live text: the confirm line while it shows, else a resting text (never empty).</summary>
    public string ConfirmAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>True when the confirm's live region is in the accessibility tree (it joins when the menu opens).</summary>
    public bool IsRegionInTree { get; private set => Set(ref field, value); }

    private bool IsAgent => _input.Kind == AttachStepKind.Agent;

    /// <summary>Takes a new input (the catalog became ready, the served confirm arrived).</summary>
    /// <param name="input">The new input.</param>
    public void Update(AttachInput input)
    {
        var moved = !string.Equals(_input.Address, input.Address, StringComparison.Ordinal);
        _input = input;
        Menu = MenuFor(input);
        foreach (var name in (string[])[nameof(PendingChipName), nameof(PendingChip), nameof(PrimaryLabel), nameof(PrimaryName), nameof(PrimaryAction), nameof(IsPending), nameof(IsPrimaryShown), nameof(HasNote), nameof(ChevronName), nameof(ConfirmLine), nameof(ConfirmButtonName), nameof(KeepButtonName)])
        {
            OnChanged(name);
        }

        if (IsConfirmingCancel && moved)
        {
            Close();   // a confirm about one Step never goes ahead against another
        }
        else if (IsConfirmingCancel)
        {
            ConfirmAnnouncement = ConfirmLine;
        }
    }

    /// <summary>Chooses an action: Cancel opens the confirm, a pending Attach does nothing, the rest are raised.</summary>
    /// <param name="action">The action.</param>
    public void Choose(AttachAction action)
    {
        if (action == AttachAction.Cancel)
        {
            IsConfirmLive = true;
            ConfirmAnnouncement = ConfirmLine;
            IsConfirmingCancel = true;
        }
        else if (action != AttachAction.Attach || !IsPending)
        {
            Chosen?.Invoke(this, new AttachChosenEventArgs(action));
        }
    }

    /// <summary>Goes ahead with the cancel the confirm asked about.</summary>
    public void ConfirmCancel()
    {
        if (!IsConfirmingCancel)
        {
            return;
        }

        Close();
        Chosen?.Invoke(this, new AttachChosenEventArgs(AttachAction.Cancel));
    }

    /// <summary>The chevron's menu opened.</summary>
    public void MenuOpened() => IsRegionInTree = true;

    /// <summary>Leaves the Step as it is.</summary>
    public void Keep() => Close();

    private void Close()
    {
        IsConfirmLive = false;
        ConfirmAnnouncement = Labels.Sindri;
        IsConfirmingCancel = false;
    }

    private static List<AttachMenuItem> MenuFor(AttachInput input)
    {
        var items = new List<AttachMenuItem> { new(AttachAction.TakeOver, Labels.TakeOver, Labels.TakeOverName(input.Address)) };
        if (input.Kind == AttachStepKind.Agent)
        {
            items.Add(new AttachMenuItem(AttachAction.FileRequest, Labels.FileRequest, Labels.FileRequestFor(input.Address)));
        }

        items.Add(new AttachMenuItem(AttachAction.Cancel, Labels.CancelStep, Labels.CancelName(input.Address)));
        return items;
    }
}
#pragma warning restore CA1822, MA0041
#pragma warning restore MA0182
