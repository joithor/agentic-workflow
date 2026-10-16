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

        ShowTabs(detail);
        var children = new ChildrenPanelViewModel(detail, clipboard, clock);
        if (Children is null || !string.Equals(Children.Signature, children.Signature, StringComparison.Ordinal))
        {
            children.Opened += (_, e) => OpenRequested?.Invoke(this, e);
            children.ItemRequested += (_, e) => ItemRequested?.Invoke(this, e);
            children.WorkRequested += (_, _) => WorkRequested?.Invoke(this, EventArgs.Empty);
            Children = children;
        }
        else
        {
            Children.Update(detail);
        }
    }

    /// <summary>Raised when the frame asks the view to put focus on one of its parts.</summary>
    internal event EventHandler<FrameFocusEventArgs>? FocusRequested;

    /// <summary>The tab strip; null before a read.</summary>
    public FrameTabsViewModel? Tabs { get; private set => Set(ref field, value); }

    /// <summary>Names what the key that opened this frame wants done once the instance is read.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="intent">The intent.</param>
    /// <param name="key">What the intent names (the child's address or its item's id for a return); null otherwise.</param>
    internal void Intend(string address, FrameIntent intent, string? key = null)
    {
        _intent = intent == FrameIntent.None ? null : (address, intent, key);
        if (Phase == FramePhase.Shown && string.Equals(Address, address, StringComparison.Ordinal))
        {
            ApplyIntent();
        }
    }

    // Once the instance is read (and only that instance): A on a desk task moves focus to the output the step awaits, X asks whether to cancel,
    // and a return from a child puts focus back on the child's entry.
    private void ApplyIntent()
    {
        if (_intent is not var (address, intent, key) || !string.Equals(address, Address, StringComparison.Ordinal))
        {
            return;
        }

        _intent = null;
        if (intent == FrameIntent.AskCancel)
        {
            Expanded?.AskCancel();
            Focus(FrameFocus.Keep, key: null);
        }
        else if (intent == FrameIntent.PendingOutput)
        {
            var awaiting = Facts?.Rows.FirstOrDefault(r => r.IsAwaiting)?.Key;
            Focus(awaiting is null ? InitialFocus : FrameFocus.PendingOutput, awaiting);
        }
        else if (ReturnTarget(intent) is { } target)
        {
            Focus(target, key);
        }
    }

    private static FrameFocus? ReturnTarget(FrameIntent intent) => intent switch
    {
        FrameIntent.FocusChild => FrameFocus.ChildEntry,
        FrameIntent.FocusChildItem => FrameFocus.ChildItem,
        FrameIntent.None or FrameIntent.PendingOutput or FrameIntent.AskCancel => null,
        _ => null,
    };

    /// <summary>Asks for focus on the frame's first read action (UI spec §11.2), unless a key already asked for another part.</summary>
    internal void FocusInitial()
    {
        if (WantedFocus is null && _intent is null)
        {
            Focus(InitialFocus, key: null);
        }
    }

    // The request is kept as well as raised: the sheet's view may not exist yet when a key opens the frame (it binds when the sheet shows).
    private void Focus(FrameFocus target, string? key)
    {
        WantedFocus = new FrameFocusEventArgs(target, key);
        FocusRequested?.Invoke(this, WantedFocus);
    }

    /// <summary>The focus a key asked for that the view has not yet given; null for none.</summary>
    internal FrameFocusEventArgs? WantedFocus { get; private set; }

    /// <summary>The view gave the focus asked for (or it can no longer be given).</summary>
    internal void FocusServed() => WantedFocus = null;

    private void ShowTabs(InstanceDetail detail)
    {
        var tabs = new FrameTabsViewModel(detail.Counts);
        if (Tabs is null || !string.Equals(Tabs.Signature, tabs.Signature, StringComparison.Ordinal))
        {
            Tabs = tabs;
        }
    }

    private void ClearPanels() => (Facts, Children, Tabs) = (null, null, null);
}
