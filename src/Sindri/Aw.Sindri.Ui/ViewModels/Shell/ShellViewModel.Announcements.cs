using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The polite live regions of the Unreadable banner, the Not responding banner and the Not running card (WCAG 4.1.3;
/// docs/csharp/sindri-ui.md "LiveSetting on macOS"). Each is a TextBlock whose live setting is its state's flag and whose
/// text is this announcement: the flag changes first and then the text, so the announcement is set while the region is
/// live and its resting text (never empty) only while it is Off.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The polite live region for a command the palette started and closed on before its outcome (always in the tree).</summary>
    public CommandOutcomeRegion CommandOutcome { get; }

    /// <summary>The Unreadable banner's live text: "Unreadable: " and the reason while unreadable, else its resting text.</summary>
    public string UnreadableAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The Not responding banner's live text: its sentence while it shows, else its resting text.</summary>
    public string NotRespondingAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The Not running card's live text: "Not running: " and the reason while not running, else its resting text.</summary>
    public string NotRunningAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The version-mismatch card's live text: its sentence while it shows, else its resting text (accessibility-tester-0, run 5).</summary>
    public string MismatchAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>True from the move back to Running after trouble (Not running, Unreadable, stale, a mismatch) until trouble comes again; the live region's flag.</summary>
    public bool IsRecovered { get; private set => Set(ref field, value); }

    /// <summary>
    /// The recovery's live text: the pill's "Running" label once Sindri is running again after trouble, else its resting
    /// text. Spec §12 has no sentence for the recovery, so it says the pill's own label (accessibility-tester-2, run 5).
    /// </summary>
    public string RecoveredAnnouncement { get; private set => Set(ref field, value); } = Labels.Sindri;

    private bool _troubled;

    // Called after the flags have been raised. Health sets a state and its reason together and raises the state first
    // (HealthViewModel.Become), so a region whose flag is still on never sees the next state's reason; an empty reason
    // while the flag holds keeps the announcement as it is.
    private void Announce()
    {
        UnreadableAnnouncement = Announcement(IsUnreadable, Labels.Unreadable, UnreadableAnnouncement);
        NotRunningAnnouncement = Announcement(IsNotRunning, Labels.NotRunning, NotRunningAnnouncement);
        NotRespondingAnnouncement = IsNotRespondingBanner ? Labels.NotResponding : Labels.Sindri;
        MismatchAnnouncement = IsVersionMismatch ? Labels.VersionMismatch : Labels.Sindri;
        AnnounceRecovery();
    }

    // Trouble is any state that isn't running and well; the move back to Normal after it is the recovery. Connecting at
    // start-up is no trouble, so nothing is said when the app first connects. The flag changes before the text.
    private void AnnounceRecovery()
    {
        if (State is ShellState.NotRunning or ShellState.Unreadable or ShellState.Stale or ShellState.VersionMismatch || IsNotRespondingBanner)
        {
            _troubled = true;
            IsRecovered = false;
            RecoveredAnnouncement = Labels.Sindri;
        }
        else if (State == ShellState.Normal && _troubled)
        {
            _troubled = false;
            IsRecovered = true;
            RecoveredAnnouncement = Labels.Running;
        }
    }

    private string Announcement(bool live, string word, string current) =>
        !live ? Labels.Sindri
        : Reason.Length > 0 ? $"{word}: {Reason}"
        : current == Labels.Sindri ? word
        : current;

    private void OnPillChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HealthPill.IsNotResponding))
        {
            OnChanged(nameof(IsNotRespondingBanner));
            Announce();
        }
    }

    private void Changed(params string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
}
