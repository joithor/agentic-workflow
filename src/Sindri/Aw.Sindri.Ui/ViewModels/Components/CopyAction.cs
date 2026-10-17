using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>
/// One Copy control's state: the label ("Copy", then "Copied" for 1.5 s) and the polite live region that says it (§11: copy
/// feedback is announced, flag first and text second, so the resting text is never spoken). It copies the text it is given.
/// </summary>
internal sealed class CopyAction : ObservableObject
{
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private readonly string _resting;
    private readonly string _restingLabel;
    private int _copies;

    internal CopyAction(IClipboard clipboard, TimeProvider clock, string resting, string? restingLabel = null)
    {
        (_clipboard, _clock, _resting, _restingLabel) = (clipboard, clock, resting, restingLabel ?? Labels.Copy);
        (Announcement, Label) = (resting, _restingLabel);
    }

    /// <summary>"Copy" (or the button's own resting words), or "Copied" for 1.5 s after a copy.</summary>
    public string Label { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the live region is live (set before its text changes).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text: "Copied", else the resting text; never empty (Avalonia #22312).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Copies the text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public async Task Run(string text, CancellationToken cancellationToken)
    {
        if (text.Length == 0 || !await _clipboard.SetText(text, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        Label = Labels.Copied;
        if (IsAnnouncing && string.Equals(Announcement, Labels.Copied, StringComparison.Ordinal))
        {
            IsAnnouncing = false;   // the same text again is still a change: dark first
        }

        if (!IsAnnouncing)
        {
            Announcement = _resting;
            IsAnnouncing = true;
        }

        Announcement = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ProtectedActionViewModel.CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            Label = _restingLabel;
            IsAnnouncing = false;
            Announcement = _resting;
        }
    }
}
