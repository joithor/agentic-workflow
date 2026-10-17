namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>
/// A polite live region's state (§11, the rule <see cref="CopyAction"/> follows): the region has a resting text that is never empty
/// (an empty announcement is a bug, Avalonia #22312), the flag goes on before the text changes, and saying the same words again goes
/// dark first, so each time is spoken once. Clearing turns the flag off and never writes an empty text.
/// </summary>
internal sealed class LiveAnnouncer : ObservableObject
{
    private readonly string _resting;

    internal LiveAnnouncer(string resting)
    {
        _resting = resting;
        Announcement = resting;
    }

    /// <summary>True while the live region is live (set before its text changes).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text: what is being said, else the resting text; never empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Says the words once.</summary>
    /// <param name="words">What to say.</param>
    internal void Say(string words)
    {
        if (IsAnnouncing && string.Equals(Announcement, words, StringComparison.Ordinal))
        {
            IsAnnouncing = false;   // the same words again are still a change: dark first
        }

        if (!IsAnnouncing)
        {
            Announcement = _resting;
            IsAnnouncing = true;
        }

        Announcement = words;
    }

    /// <summary>Stops announcing: the flag goes off, the text rests.</summary>
    internal void Quiet()
    {
        IsAnnouncing = false;
        Announcement = _resting;
    }
}
