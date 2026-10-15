using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Components;

#pragma warning disable MA0182 // until the U8 pages place the component
#pragma warning disable CA1822, MA0041 // fixed sentences are properties because the view binds them
/// <summary>
/// The lock button and its popover (screens fixture "The protected-action label", rev 8 critique run 5 P1): a protected
/// action is never a dead end. The popover shows the CLI the catalog row serves, a Copy, and Open in an agent. It only
/// shows and copies: it has no way to send a command (its ports are the clipboard and the clock). Open in an agent has
/// no defined action in the rev-8 spec yet, so it shows as pending with the phase that serves agent sessions.
/// </summary>
/// <param name="action">The verb, the served CLI and the subject.</param>
/// <param name="clipboard">Where Copy puts the CLI.</param>
/// <param name="clock">The clock for "Copied".</param>
internal sealed class ProtectedActionViewModel(ProtectedAction action, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>How long "Copied" shows.</summary>
    internal static readonly TimeSpan CopiedFor = ControlViewModel.CopiedFor;

    private int _copies;

    /// <summary>The button's label: "⌂ Accept · terminal".</summary>
    public string Label => Labels.ProtectedLabel(action.Verb);

    /// <summary>The button's title.</summary>
    public string Title => Labels.ProtectedTitle;

    /// <summary>The button's accessible name, which carries the subject.</summary>
    public string ButtonName => Labels.ProtectedName(action.Verb, action.Subject);

    /// <summary>The served CLI line, or empty when it holds a control or bidi character.</summary>
    public string Cli { get; } = ControlViewModel.Safe(action.Cli) ? action.Cli : string.Empty;

    /// <summary>True when the CLI can be copied.</summary>
    public bool CanCopy => Cli.Length > 0;

    /// <summary>True while the popover shows.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>"Copy", or "Copied" for 1.5 s after a copy.</summary>
    public string CopyLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyName));
        }
    } = Labels.Copy;

    /// <summary>The Copy button's accessible name: "Copy: sindri …", then "Copied".</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Cli;

    /// <summary>True while the live region is live (set before its text changes): the resting label is never spoken.</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The polite live region's text: "Copied", else a resting text; never empty (Avalonia #22312).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Copy;

    /// <summary>Open in an agent's label.</summary>
    public string OpenInAgentLabel => Labels.OpenInAgent;

    /// <summary>Open in an agent's accessible name, which carries the subject.</summary>
    public string OpenInAgentName => Labels.OpenInAgentFor(action.Subject);

    /// <summary>True: the rev-8 spec defines no action for Open in an agent yet, so it shows as a pending control.</summary>
    public bool IsOpenInAgentPending => true;

    /// <summary>The dashed chip that stands for Open in an agent: the phase that serves agent sessions (as Attach's).</summary>
    public string OpenInAgentChip => Labels.AvailableWithP10a;

    /// <summary>Shows or hides the popover.</summary>
    /// <param name="open">True to show it.</param>
    public void SetOpen(bool open) => IsOpen = open;

    /// <summary>Copies the CLI: "Copied" for 1.5 s, announced flag first, then text.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public async Task Copy(CancellationToken cancellationToken)
    {
        if (!CanCopy || !await clipboard.SetText(Cli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        Announce(Labels.Copied);
        _ = Uncopy(++_copies, cancellationToken);
    }

    // Flag first, then text: the region is live only while something is announced, so the resting text is never spoken.
    private void Announce(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;   // the same text again (a second Copy) is still a change: dark first
        }

        if (!IsAnnouncing)
        {
            Announcement = Label;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CopiedFor, clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
            IsAnnouncing = false;
        }
    }
}
#pragma warning restore CA1822, MA0041
#pragma warning restore MA0182
