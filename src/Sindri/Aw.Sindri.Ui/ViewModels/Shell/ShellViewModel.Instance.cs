using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The instance frame in the shell (<see cref="ShellViewModel"/>, continued; U8 T4): a workflow link opens it in the sheet, read by id when a row, the strip or a child pointer knew the id.</summary>
internal sealed partial class ShellViewModel
{
    private readonly Dictionary<string, string> _hintedIds = new(StringComparer.Ordinal);
    private readonly Stack<(DeepLink Link, bool Pointer)> _trail = new();
    private bool _stacking;

    /// <summary>The instance frame.</summary>
    public InstanceFrameViewModel Instance { get; }

    /// <summary>True when the sheet's subject is a workflow instance (its own view in the sheet's content).</summary>
    public bool IsInstanceSheet => SheetLink?.Kind == DeepLinkKind.Workflow;

    /// <summary>The sheet's width as it is shown: an instance's is the widest, every other sheet keeps the human's.</summary>
    public double ShownSheetWidth => IsInstanceSheet || IsRecipeSheet ? UiPreferences.MaxSheetWidth : SheetWidth;

    /// <summary>True when the sheet was stacked from a frame: its Back has a frame to return to, at every width.</summary>
    public bool HasTrail => _trail.Count > 0;

    /// <summary>The Workflows row the frame was opened from (its address), where Esc returns focus; null when it came from elsewhere.</summary>
    internal string? OpenerWorkflow { get; private set; }

    /// <summary>Raised when a sheet was opened from inside a frame (an item): the control that had focus left the tree, so the view puts focus on the sheet's heading.</summary>
    internal event EventHandler? SheetStacked;

    /// <summary>Opens an instance's frame.</summary>
    /// <param name="address">The address.</param>
    /// <param name="id">Its id when the caller knows it (so the read needs no lookup); null otherwise.</param>
    internal void OpenInstance(string address, string? id)
    {
        if (id is not null)
        {
            _hintedIds[address] = id;
        }

        Navigate(new DeepLink(DeepLinkKind.Workflow, address));
    }

    /// <summary>Back: returns to the frame or sheet this one was opened from (a child, an item), else closes the sheet like Esc does.</summary>
    internal void Back()
    {
        if (IsSheetOpen && _trail.TryPop(out var previous))
        {
            OnChanged(nameof(HasTrail));
            ReturnFocus(previous.Link, SheetLink, previous.Pointer);
            Stacked(() => Navigate(previous.Link), push: false);
            return;
        }

        ClearTrail();
        CloseSheet();
    }

    // Back to a frame lands focus on the entry of the child (or the child's item) the user went into, or on the Recipe link that opened the recipe, once the frame is shown.
    private void ReturnFocus(DeepLink previous, DeepLink? leaving, bool viaPointer)
    {
        if (previous is not { Kind: DeepLinkKind.Workflow, Subject: { } parent } || leaving is not { Subject: { } from })
        {
            return;
        }

        if (leaving.Kind == DeepLinkKind.Recipe)
        {
            Instance.Intend(parent, FrameIntent.FocusRecipe);
        }
        else if (leaving.Kind is DeepLinkKind.Workflow or DeepLinkKind.Item)
        {
            Instance.Intend(parent, leaving.Kind == DeepLinkKind.Item ? FrameIntent.FocusChildItem : viaPointer ? FrameIntent.FocusPointer : FrameIntent.FocusChild, from);
        }
    }

    // Everything the frame needs once it exists (ShellViewModel's constructor calls this one line).
    private void InitInstance()
    {
        Health.Follow((url, token) => IsInstanceSheet ? Instance.Refresh(url, token) : Task.CompletedTask);
        Instance.OpenRequested += (_, e) =>
        {
            Instance.Intend(e.Address, FrameIntent.OpenInitial);   // the child's frame, once read, asks for its first read action
            Stacked(() => OpenInstance(e.Address, e.Id), push: true, e.FromPointer);
        };
        Instance.ItemRequested += (_, e) =>
        {
            Stacked(() => Navigate(new DeepLink(DeepLinkKind.Item, e.ItemId)), push: true);
            SheetStacked?.Invoke(this, EventArgs.Empty);
        };
        Instance.WorkRequested += (_, _) => OpenWork();
    }

    // The instance part of opening the sheet (OnSheetOpened): the frame names its instance at once and reads it; the id is the one a row or
    // the strip showed, else the list resolves the address.
    private void OnInstanceSheetOpened(DeepLink link)
    {
        Workflows.ClearNote();
        if (!_stacking)
        {
            ClearTrail();   // a sheet opened from anywhere but a frame starts a new trail
            OpenerWorkflow = null;   // ... and has no Workflows row to return to; a stacked one keeps the row that opened the first frame
        }

        Changed(nameof(IsInstanceSheet), nameof(ShownSheetWidth));
        if (link.Kind != DeepLinkKind.Workflow || link.Subject is not { } address)
        {
            return;
        }

        var id = KnownId(address);
        Instance.Show(address, id);
        if (Health.ApiUrl() is { } url)
        {
            _ = Instance.Open(url, address, id, CancellationToken.None);
        }
    }

    private void ClearTrail()
    {
        _trail.Clear();
        OnChanged(nameof(HasTrail));
    }

    // A sheet opened from inside a frame (a child, an item) keeps the one it came from, so Back returns to it.
    private void Stacked(Action open, bool push, bool viaPointer = false)
    {
        if (push && IsSheetOpen && SheetLink is { } here)
        {
            _trail.Push((here, viaPointer));
            OnChanged(nameof(HasTrail));
        }

        _stacking = true;
        try
        {
            open();
        }
        finally
        {
            _stacking = false;
        }
    }

    // "Open all in Work": the Work page, unfiltered, with the frame closed.
    private void OpenWork()
    {
        ClearTrail();
        CloseSheet();
        Queue.ClearFilter();   // every item of the group, not the ones a filter left over would show
        GoTo(PageId.Work);
    }

    private string? KnownId(string address) =>
        _hintedIds.TryGetValue(address, out var hinted) ? hinted
        : Workflows.Groups.SelectMany(g => g.Rows).FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal))?.InstanceId
            ?? Strip.Rows.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal))?.InstanceId;
}
