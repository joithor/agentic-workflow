using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The state the instance is in, expanded: the engine's status layout (WF §6.4) as controls (screens §4.2). Every line is a
/// served field mapped to words, and the next sentence is shown whole. Nothing is sent from here yet (U8b): each command
/// shows its CLI with a Copy, a protected one is the lock button, and Cancel asks first with Keep focused.
/// </summary>
internal sealed class ExpandedStateViewModel : ObservableObject
{
    private CurrentState _current;
    private readonly string _address;
    private readonly ExpandedCommandViewModel? _cancel;

    internal ExpandedStateViewModel(CurrentState current, string address, IClipboard clipboard, TimeProvider clock)
    {
        (_current, _address) = (current, address);
        var status = current.Status;
        Commands = [.. status.Commands.Select(c => new ExpandedCommandViewModel(c, status.Answer?.Kind, address, clipboard, clock))];
        _cancel = Commands.FirstOrDefault(c => c.Verb == StatusVerb.Cancel);
        Gates = [.. current.Gates.Select(Labels.GateText)];
        StepLine = Line(current.Step, Labels.StepLine);
        (BudgetLine, TimeoutLine, PausedLine, DriftLine, Next) = Moving(current);
        if (status.Child is { } child)
        {
            Child = new ChildPointerViewModel(child);
            Child.Opened += (_, e) => ChildRequested?.Invoke(this, e);
        }

        HasAttach = current.Step is { } step && string.Equals(step.Ref, "step.agent", StringComparison.Ordinal) && status.Paused is null;
        Name = Labels.ExpandedName(current.State, address);
        CopyStatusName = Labels.CopyStatusName(address);
        StatusCopy = new CopyAction(clipboard, clock, Labels.CopyStatus);
    }

    // The lines that move with time or with the engine's counters: a re-read changes their words in place and keeps the controls.
    private static (string Budget, string Timeout, string Paused, string Drift, string Next) Moving(CurrentState current)
    {
        var status = current.Status;
        return (Line(status.Budget, Labels.BudgetLine), Line(status.Timeout, Labels.TimeoutLine), Line(status.Paused, p => Labels.PausedLine(p.TimeoutLeftSeconds)), Line(status.Drift, Labels.DriftLine), status.Next ?? string.Empty);
    }

    private static string Line<T>(T? served, Func<T, string> words)
        where T : class => served is null ? string.Empty : words(served);

    /// <summary>Raised when the child pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? ChildRequested;

    /// <summary>The state.</summary>
    public string State => _current.State;

    /// <summary>The step line ("kind · ref · declaration"), or empty.</summary>
    public string StepLine { get; }

    /// <summary>True when the state has a step.</summary>
    public bool HasStep => StepLine.Length > 0;

    /// <summary>The exit gates, in served order.</summary>
    public IReadOnlyList<string> Gates { get; }

    /// <summary>True when there are gates.</summary>
    public bool HasGates => Gates.Count > 0;

    /// <summary>The served next sentence, whole; empty when none.</summary>
    public string Next { get; private set => Set(ref field, value); }

    /// <summary>True when a next sentence is served.</summary>
    public bool HasNext => Next.Length > 0;

    /// <summary>The budget line, or empty.</summary>
    public string BudgetLine { get; private set => Set(ref field, value); }

    /// <summary>True when a budget is served.</summary>
    public bool HasBudget => BudgetLine.Length > 0;

    /// <summary>The timeout line, or empty.</summary>
    public string TimeoutLine { get; private set => Set(ref field, value); }

    /// <summary>True when a timeout is served.</summary>
    public bool HasTimeout => TimeoutLine.Length > 0;

    /// <summary>The paused line, or empty.</summary>
    public string PausedLine { get; private set => Set(ref field, value); }

    /// <summary>True while the instance is taken over.</summary>
    public bool IsPaused => PausedLine.Length > 0;

    /// <summary>The drift line, or empty.</summary>
    public string DriftLine { get; private set => Set(ref field, value); }

    /// <summary>True when the recipe drifted.</summary>
    public bool HasDrift => DriftLine.Length > 0;

    /// <summary>The pointer to the child the instance waits on, or null.</summary>
    public ChildPointerViewModel? Child { get; }

    /// <summary>The commands in served order.</summary>
    public IReadOnlyList<ExpandedCommandViewModel> Commands { get; }

    /// <summary>True when there are commands.</summary>
    public bool HasCommands => Commands.Count > 0;

    /// <summary>True when one of the commands is an answer.</summary>
    public bool HasAnswer => Commands.Any(c => c.Verb == StatusVerb.Answer);

    /// <summary>True on a live agent step, which Attach joins once API P10a serves it.</summary>
    public bool HasAttach { get; }

    /// <summary>The chip that stands for Attach; empty without one.</summary>
    public string AttachChip => HasAttach ? Labels.AvailableWithP10a : string.Empty;

    /// <summary>The Attach chip's accessible name.</summary>
    public string AttachName => HasAttach ? Labels.AttachPendingName(_address) : string.Empty;

    /// <summary>True while the Cancel confirm shows.</summary>
    public bool IsConfirmingCancel { get; private set => Set(ref field, value); }

    /// <summary>The confirm line: the served sentence, else "Cancel &lt;address&gt;?".</summary>
    public string ConfirmLine => _cancel?.Confirm is { Length: > 0 } served ? served : Labels.CancelAsk(_address);

    /// <summary>Where focus lands in the confirm: Keep, the safe answer.</summary>
#pragma warning disable CA1822, MA0041 // a fixed answer is a property because the view reads it
    public ConfirmChoice ConfirmFocus => ConfirmChoice.Keep;

    /// <summary>The Keep button's words.</summary>
    public string KeepLabel => Labels.KeepStep;

    /// <summary>The go-ahead button's words.</summary>
    public string ConfirmCancelLabel => Labels.ConfirmCancel;
#pragma warning restore CA1822, MA0041

    /// <summary>The confirm's accessible name for going ahead.</summary>
    public string ConfirmCancelName => Labels.ConfirmCancelName(_address);

    /// <summary>The confirm's accessible name for keeping.</summary>
    public string KeepName => Labels.KeepName(_address);

    /// <summary>What the frame says after a confirmed cancel, which the app does not send yet; empty otherwise.</summary>
    public string PendingNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The live region that says the pending note: flag first, text second, never empty.</summary>
    public LiveAnnouncer PendingVoice { get; } = new(Labels.PendingNoteResting);

    /// <summary>The Copy status control: its words and the live region that says "Copied".</summary>
    public CopyAction StatusCopy { get; }

    /// <summary>The Copy status button's accessible name.</summary>
    public string CopyStatusName { get; }

    /// <summary>The expanded state's accessible name.</summary>
    public string Name { get; }

    /// <summary>
    /// What the state draws as controls and structure, so a re-read of the same keeps them (and the focus, the open confirm and a Copied label).
    /// The child pointer's drawn words are in it (the child's state moves them). The words that move with time (the status text, the timeout, the budget, the next sentence) are not: <see cref="Update"/> changes them in place.
    /// </summary>
    internal string Signature => string.Join('|', _current.State, StepLine, string.Join(',', Gates), string.Join(',', Commands.Select(c => c.Signature)), _current.Status.Child is { } child ? $"{child.Id}={Labels.ChildLine(child)}" : null, HasNext, HasBudget, HasTimeout, IsPaused, HasDrift);

    /// <summary>Takes a re-read of the same state: the moving words change, the controls stay.</summary>
    /// <param name="current">The state as served now; its <see cref="Signature"/> equals this one's.</param>
    internal void Update(CurrentState current)
    {
        _current = current;
        (BudgetLine, TimeoutLine, PausedLine, DriftLine, Next) = Moving(current);
    }

    /// <summary>Asks to cancel: shows the confirm. Without a Cancel command there is nothing to ask.</summary>
    public void AskCancel()
    {
        if (_cancel is not null)
        {
            PendingVoice.Quiet();
            PendingNote = string.Empty;
            IsConfirmingCancel = true;
        }
    }

    /// <summary>Keeps the instance: closes the confirm.</summary>
    public void Keep() => IsConfirmingCancel = false;

    /// <summary>Goes ahead with the cancel the confirm asked about. The app sends nothing yet: it says so, and the Cancel command's CLI is above to copy.</summary>
    public void ConfirmCancel()
    {
        if (IsConfirmingCancel)
        {
            IsConfirmingCancel = false;
            PendingNote = Labels.SendingArrivesNote;
            PendingVoice.Say(Labels.SendingArrivesNote);
        }
    }

    /// <summary>Copies the engine's status text, whole.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyStatus(CancellationToken cancellationToken) => StatusCopy.Run(_current.Status.Text, cancellationToken);
}
