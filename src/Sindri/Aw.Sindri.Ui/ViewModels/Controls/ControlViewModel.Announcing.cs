namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>The control's polite live region (<see cref="ControlViewModel"/>, continued): what it says and when it is live.</summary>
internal sealed partial class ControlViewModel
{
    /// <summary>True while "Sending…" is announced (the live region's gate, set before its text changes).</summary>
    public bool IsAnnouncingSending { get; private set => Set(ref field, value); }

    /// <summary>True while the live region is live (its gate, set before its text changes): the resting label is never spoken.</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>
    /// True while something else speaks this control's lifecycle (the command palette, open over it): the control's own live
    /// region stays dark, so a run from the palette is announced once. Clearing it lets the next announcement through.
    /// </summary>
    public bool IsMuted
    {
        get;
        internal set
        {
            Set(ref field, value);
            if (value)
            {
                IsAnnouncing = false;
            }
        }
    }

    /// <summary>The polite live region's text: "Sending…", the outcome, "Copied"; never empty (Avalonia #22312).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Copy;

    // Says a text through the live region. The region is live only while something is announced (flag first, then the
    // text, as the shell's regions do): the first announcement rests the text on the label with the region still dark, so
    // the resting label is never spoken (review run 2, a11y-1).
    private void Announce(string text)
    {
        if (IsMuted)
        {
            Announcement = text;   // unspoken: the region is dark
            return;
        }

        if (!IsAnnouncing)
        {
            Announcement = Label;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    // Back to rest: the region goes dark again (its text stays, unspoken) once nothing is on show or in flight.
    private void Rest()
    {
        if (Phase is CommandPhase.Idle && CopyLabel == Labels.Copy)
        {
            IsAnnouncing = false;
        }
    }
}
