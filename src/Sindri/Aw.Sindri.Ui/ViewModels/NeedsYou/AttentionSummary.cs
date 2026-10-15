using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0182 // until the shell places it (U3' T5)

/// <summary>
/// What the needs-you rows add up to outside the list (UI spec §5): the window title, the dock or taskbar badge, the tray
/// and Home's summary line. Counts and words only; every row counts, calm ones too, and the tray's own states (stale,
/// not running) win over the counts.
/// </summary>
/// <param name="Title">The window title.</param>
/// <param name="Badge">The dock or taskbar badge; 0 clears it.</param>
/// <param name="Tray">What the tray shows.</param>
/// <param name="SummaryLine">Home's summary line; empty before the list is known.</param>
internal sealed record AttentionSummary(string Title, int Badge, TrayView Tray, string SummaryLine)
{
    /// <summary>Works out the summary.</summary>
    /// <param name="feed">The needs-you list, or null before it is known.</param>
    /// <param name="state">The shell's state.</param>
    /// <param name="now">The time the ages are read at.</param>
    /// <returns>The summary.</returns>
    internal static AttentionSummary For(NeedsYouFeed? feed, ShellState state, DateTimeOffset now) => throw new NotSupportedException();
}
