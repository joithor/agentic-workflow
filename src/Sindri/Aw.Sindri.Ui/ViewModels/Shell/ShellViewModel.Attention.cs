using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>What the needs-you rows add up to outside the list (UI spec §5): the window title, the dock badge, the tray, the live region.</summary>
internal sealed partial class ShellViewModel
{
    private readonly AttentionAnnouncer _announcer = new();
    private NeedsYouFeed? _needsYou;

    /// <summary>The window's title: "(N) Needs you · Sindri" while rows need the user, else "Sindri".</summary>
    public string WindowTitle => Attention.Title;

    /// <summary>The summary the title, the dock badge and the tray follow; it changes with the list and with the shell's state.</summary>
    internal AttentionSummary Attention { get; private set; }

    /// <summary>True once the polite live region has something to say (the flag changes before the text).</summary>
    public bool IsNeedsYouAnnouncing => _announcer.Line.Length > 0;

    /// <summary>The polite live region's line: "N now need you" once for each rise, else its resting text.</summary>
    public string NeedsYouAnnouncement => _announcer.Line.Length > 0 ? _announcer.Line : Labels.Sindri;

    /// <summary>Takes the needs-you list as the API serves it now.</summary>
    /// <param name="feed">The list.</param>
    internal void ShowNeedsYou(NeedsYouFeed feed)
    {
        _needsYou = feed;
        UpdateNeedsYou();
        Reattend();
    }

    // Works the summary out again from the last list and the current state; the title, the badge and the tray follow it.
    private void Reattend()
    {
        var before = _announcer.Line;
        Attention = AttentionSummary.For(_needsYou, State, _clock.GetUtcNow());
        _announcer.Observe(Attention.Badge);
        Changed(nameof(Attention), nameof(WindowTitle));
        if (!string.Equals(before, _announcer.Line, StringComparison.Ordinal) || _announcer.Announcements > 0)
        {
            OnChanged(nameof(IsNeedsYouAnnouncing));
            OnChanged(nameof(NeedsYouAnnouncement));
        }
    }
}
