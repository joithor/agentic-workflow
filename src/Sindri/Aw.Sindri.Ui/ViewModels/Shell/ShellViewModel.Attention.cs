using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

#pragma warning disable MA0041, CA1822, MA0182 // red stubs (U3' T5)

/// <summary>What the needs-you rows add up to outside the list (UI spec §5): the window title, the dock badge, the tray, the live region.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The window's title: "(N) Needs you · Sindri" while rows need the user, else "Sindri".</summary>
    public string WindowTitle => throw new NotSupportedException();

    /// <summary>The summary the title, the dock badge and the tray follow.</summary>
    internal AttentionSummary Attention => throw new NotSupportedException();

    /// <summary>The polite live region's line: "N now need you" once for each rise, else its resting text.</summary>
    public string NeedsYouAnnouncement => throw new NotSupportedException();

    /// <summary>Takes the needs-you list as the API serves it now.</summary>
    /// <param name="feed">The list.</param>
    internal void ShowNeedsYou(NeedsYouFeed feed) => throw new NotSupportedException();
}
