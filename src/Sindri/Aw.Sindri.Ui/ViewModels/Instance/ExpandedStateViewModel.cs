#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The state the instance is in, expanded: the engine's status layout as controls.</summary>
internal sealed class ExpandedStateViewModel : ObservableObject
{
    internal ExpandedStateViewModel(CurrentState current, string address, IClipboard clipboard, TimeProvider clock) => throw new NotSupportedException();

    /// <summary>Raised when the child pointer is followed.</summary>
    internal event EventHandler<FrameOpenEventArgs>? ChildRequested;

    /// <summary>The state.</summary>
    public string State => throw new NotSupportedException();

    /// <summary>The step line, or empty.</summary>
    public string StepLine => throw new NotSupportedException();

    /// <summary>True when the state has a step.</summary>
    public bool HasStep => throw new NotSupportedException();

    /// <summary>The exit gates.</summary>
    public IReadOnlyList<string> Gates => throw new NotSupportedException();

    /// <summary>True when there are gates.</summary>
    public bool HasGates => throw new NotSupportedException();

    /// <summary>The served next sentence, whole; empty when none.</summary>
    public string Next => throw new NotSupportedException();

    /// <summary>True when a next sentence is served.</summary>
    public bool HasNext => throw new NotSupportedException();

    /// <summary>The budget line, or empty.</summary>
    public string BudgetLine => throw new NotSupportedException();

    /// <summary>True when a budget is served.</summary>
    public bool HasBudget => throw new NotSupportedException();

    /// <summary>The timeout line, or empty.</summary>
    public string TimeoutLine => throw new NotSupportedException();

    /// <summary>True when a timeout is served.</summary>
    public bool HasTimeout => throw new NotSupportedException();

    /// <summary>The paused line, or empty.</summary>
    public string PausedLine => throw new NotSupportedException();

    /// <summary>True while the instance is taken over.</summary>
    public bool IsPaused => throw new NotSupportedException();

    /// <summary>The drift line, or empty.</summary>
    public string DriftLine => throw new NotSupportedException();

    /// <summary>True when the recipe drifted.</summary>
    public bool HasDrift => throw new NotSupportedException();

    /// <summary>The pointer to the child the instance waits on, or null.</summary>
    public ChildPointerViewModel? Child => throw new NotSupportedException();

    /// <summary>The commands in served order.</summary>
    public IReadOnlyList<ExpandedCommandViewModel> Commands => throw new NotSupportedException();

    /// <summary>True when there are commands.</summary>
    public bool HasCommands => throw new NotSupportedException();

    /// <summary>True when one of the commands is an answer.</summary>
    public bool HasAnswer => throw new NotSupportedException();

    /// <summary>The chip that stands for Attach on a live agent step; empty otherwise.</summary>
    public string AttachChip => throw new NotSupportedException();

    /// <summary>True on a live agent step.</summary>
    public bool HasAttach => throw new NotSupportedException();

    /// <summary>The Attach chip's accessible name.</summary>
    public string AttachName => throw new NotSupportedException();

    /// <summary>True while the Cancel confirm shows.</summary>
    public bool IsConfirmingCancel => throw new NotSupportedException();

    /// <summary>The confirm line: the served sentence, else "Cancel &lt;address&gt;?".</summary>
    public string ConfirmLine => throw new NotSupportedException();

    /// <summary>Where focus lands in the confirm: Keep.</summary>
    public ConfirmChoice ConfirmFocus => throw new NotSupportedException();

    /// <summary>What the frame says after a confirmed send, which the app does not make yet; empty otherwise.</summary>
    public string PendingNote => throw new NotSupportedException();

    /// <summary>The Copy status button's words.</summary>
    public string CopyStatusLabel => throw new NotSupportedException();

    /// <summary>The Copy status button's accessible name.</summary>
    public string CopyStatusName => throw new NotSupportedException();

    /// <summary>The expanded state's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>Asks to cancel: shows the confirm.</summary>
    public void AskCancel() => throw new NotSupportedException();

    /// <summary>Keeps the instance: closes the confirm.</summary>
    public void Keep() => throw new NotSupportedException();

    /// <summary>Goes ahead with the cancel the confirm asked about; the app sends nothing yet.</summary>
    public void ConfirmCancel() => throw new NotSupportedException();

    /// <summary>Copies the engine's status text, whole.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyStatus(CancellationToken cancellationToken) => throw new NotSupportedException();
}
