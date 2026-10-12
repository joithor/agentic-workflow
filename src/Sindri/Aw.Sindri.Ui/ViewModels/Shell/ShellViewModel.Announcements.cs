namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The polite live regions of the Unreadable banner, the Not responding banner and the Not running card (WCAG 4.1.3;
/// docs/csharp/sindri-ui.md "LiveSetting on macOS"). Each is a TextBlock whose live setting is its state's flag and whose
/// text is this announcement: the flag changes first and then the text, so the announcement is set while the region is
/// live and its resting text (never empty) only while it is Off.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Unreadable banner's live text: "Unreadable: " and the reason while unreadable, else its resting text.</summary>
    public string UnreadableAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The Not responding banner's live text: its sentence while it shows, else its resting text.</summary>
    public string NotRespondingAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The Not running card's live text: "Not running: " and the reason while not running, else its resting text.</summary>
    public string NotRunningAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    // Called after the flags have been raised. A reason emptied on the way out of a state (Health sets the reason before
    // the state) keeps the announcement as it is, so nothing is spoken until the flag is off and the text rests.
    private void Announce()
    {
        UnreadableAnnouncement = Announcement(IsUnreadable, Labels.Unreadable, UnreadableAnnouncement);
        NotRunningAnnouncement = Announcement(IsNotRunning, Labels.NotRunning, NotRunningAnnouncement);
        NotRespondingAnnouncement = IsNotRespondingBanner ? Labels.NotResponding : Labels.Sindri;
    }

    private string Announcement(bool live, string word, string current) =>
        !live ? Labels.Sindri
        : Reason.Length > 0 ? $"{word}: {Reason}"
        : current == Labels.Sindri ? word
        : current;
}
