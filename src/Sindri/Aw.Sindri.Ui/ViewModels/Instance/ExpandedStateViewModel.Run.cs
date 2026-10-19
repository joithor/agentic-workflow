using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The expanded state's agent run (<see cref="ExpandedStateViewModel"/>, continued): the run line, the launch's state, Attach and what the agent's Steps came to.</summary>
internal sealed partial class ExpandedStateViewModel
{
    private ItemView? _item;
    private bool _itemUnreadable;

    /// <summary>Sends Attach; set by the frame, null when nothing can be sent.</summary>
    internal AgentAttacher? Attacher { get; set; }

    /// <summary>The Attach component on a live agent Step (an agent run, or a step.agent Step not taken over); null otherwise.</summary>
    public AttachControlViewModel? Attach { get; private set; }

    /// <summary>True when the Attach component shows.</summary>
    public bool HasAttach => Attach is not null;

    /// <summary>"Agent · LCH-…" when an agent session runs the Step; empty otherwise.</summary>
    public string RunLine { get; private set; } = string.Empty;

    /// <summary>True when an agent runs the Step.</summary>
    public bool HasRun => RunLine.Length > 0;

    /// <summary>The run line's accessible name.</summary>
    public string RunName { get; private set; } = string.Empty;

    /// <summary>The launch's state in words; empty until it is read.</summary>
    public string LaunchLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the launch's state shows.</summary>
    public bool HasLaunchLine => LaunchLine.Length > 0;

    /// <summary>What the agent's Steps came to ("via agent · 1,200 tokens", "The session was lost. …"), oldest first.</summary>
    public IReadOnlyList<string> RunEvents { get; private set => Set(ref field, value); } = [];

    /// <summary>True when there are run events to show.</summary>
    public bool HasRunEvents => RunEvents.Count > 0;

    /// <summary>What the last Attach came to, in words; empty before one.</summary>
    public string AttachOutcomeLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The attach command the print viewer left to run, or empty.</summary>
    public string AttachCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is an attach command to show.</summary>
    public bool HasAttachCommand => AttachCommand.Length > 0;

    /// <summary>The attach command's Copy.</summary>
    public CopyAction AttachCommandCopy { get; }

    /// <summary>The attach command's Copy button name.</summary>
    public string CopyAttachName { get; }

    private void InitRun(CurrentState current, string address)
    {
        if (current.Status.Run is { } run && string.Equals(run.Runtime, "agent", StringComparison.Ordinal))
        {
            (RunLine, RunName) = (Labels.RunLine(run.Launch), Labels.RunName(run.Launch));
        }

        var live = HasRun || (current.Step is { } step && string.Equals(step.Ref, "step.agent", StringComparison.Ordinal) && current.Status.Paused is null);
        if (live)
        {
            Attach = new AttachControlViewModel(AttachFor(address));
            Attach.Chosen += (_, e) => _ = Chosen(e.Action);
        }
    }

    private AttachInput AttachFor(string address)
    {
        var reason = RunWords.PendingReason(_item, _itemUnreadable);
        return new AttachInput(address, AttachStepKind.Agent, reason is null, _cancel?.Confirm, reason);
    }

    /// <summary>Takes what the frame read about the run: the launch's state, the agent Steps' events, and whether Attach is ready.</summary>
    /// <param name="read">What was read.</param>
    internal void ShowRun(AgentRunRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        (_item, _itemUnreadable) = (read.Item, read.ItemUnreadable);
        LaunchLine = HasRun ? (read.Launch is { } launch ? RunWords.LaunchLine(launch) : Labels.LaunchUnreadable) : string.Empty;
        OnChanged(nameof(HasLaunchLine));
        RunEvents = RunWords.EventLines(read.Events);
        OnChanged(nameof(HasRunEvents));
        Attach?.Update(AttachFor(_address));
    }

    private async Task Chosen(AttachAction action)
    {
        if (action == AttachAction.Attach)
        {
            await SendAttach().ConfigureAwait(true);
            return;
        }

        // The app sends no command yet (U8b): a takeover's CLI is copied; Cancel (the component asked first, with the served
        // sentence) and File a request say the send arrives later.
        if (action == AttachAction.TakeOver && Commands.FirstOrDefault(c => c.Verb == StatusVerb.Takeover) is { } takeover)
        {
            await takeover.Copy(CancellationToken.None).ConfigureAwait(true);
        }

        Pending();
    }

    private void Pending()
    {
        PendingNote = Labels.SendingArrivesNote;
        PendingVoice.Say(Labels.SendingArrivesNote);
    }

    private async Task SendAttach()
    {
        if (Attacher is not { } attacher || _item is not { } item)
        {
            return;
        }

        using var source = new CancellationTokenSource();
        var outcome = await attacher.Attach(item.Version, source.Token).ConfigureAwait(true);
        (AttachOutcomeLine, AttachCommand) = (outcome.Line, outcome.Command);
        OnChanged(nameof(HasAttachCommand));
        PendingVoice.Say(outcome.Line);
    }

    /// <summary>Copies the attach command, whole.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyAttachCommand(CancellationToken cancellationToken) => AttachCommandCopy.Run(AttachCommand, cancellationToken);
}
