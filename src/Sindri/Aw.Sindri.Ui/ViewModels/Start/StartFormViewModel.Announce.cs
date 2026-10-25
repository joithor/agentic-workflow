namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>The Start form's live region and state notifications (<see cref="StartFormViewModel"/>, continued).</summary>
internal sealed partial class StartFormViewModel
{
    // Says words through the polite region, as CreateItemViewModel does: dark first, the resting text set, then live, then the words (the flag changes before the
    // text), so the resting text is never spoken and the same words are spoken again. Nothing to say says nothing.
    private void Say(string words)
    {
        if (words.Length == 0)
        {
            return;
        }

        if (IsAnnouncing && string.Equals(Announcement, words, StringComparison.Ordinal))
        {
            IsAnnouncing = false;
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.StartWorkflow;
            IsAnnouncing = true;
        }

        Announcement = words;
    }

    // A new opening starts at rest: the region dark with its resting text, nothing left over from the last one.
    private void Rest()
    {
        IsAnnouncing = false;
        Announcement = Labels.StartWorkflow;
    }

    // The things that decide Start and the busy line changed together.
    private void StateChanged()
    {
        OnChanged(nameof(CanStart));
        OnChanged(nameof(StartHelp));
        OnChanged(nameof(IsBusy));
        OnChanged(nameof(BusyText));
    }
}
