using System.Globalization;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the tray, Settings, and page names.</summary>
internal static partial class Labels
{
    // The tray (§5). U0 knows no counts; U2/U3 add "Sindri · N running · nothing needs you" and the needs-you tooltips.

    /// <summary>The tray's Neutral tooltip in U0.</summary>
    internal const string TrayRunning = "Sindri · running";

    /// <summary>The tray's Stale tooltip.</summary>
    internal const string TrayNotResponding = "Sindri not responding";

    /// <summary>The tray's Not running tooltip.</summary>
    internal const string TrayNotRunning = "Sindri isn't running · run sindri";

    /// <summary>The tray menu's open item.</summary>
    internal const string TrayOpen = "Open Sindri";

    /// <summary>The tray menu's quit item.</summary>
    internal const string TrayQuit = "Quit";

    // Settings (§3.3 ⌘,, §11.4, §14.2).

    /// <summary>The in-app switch that forces reduced motion.</summary>
    internal const string ReduceMotion = "Reduce motion";

    /// <summary>The note beside the switch while the OS reduces motion (the switch can't turn the OS setting off).</summary>
    internal const string SystemReducesMotion = "On in your system settings";

    /// <summary>The text size slider.</summary>
    internal const string TextSize = "Text size";

    /// <summary>A page's name, as the top bar and the page region's accessible name say it (§11.2).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The words.</returns>
    internal static string Page(PageId page) => page switch
    {
        PageId.Home => Home,
        PageId.Queue => Queue,
        PageId.Decisions => Decisions,
        PageId.Services => Services,
        PageId.Health => Health,
        PageId.Settings => Settings,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, message: null),
    };

    /// <summary>A scale as a percentage ("110%").</summary>
    /// <param name="scale">The scale (1.0 is 100%).</param>
    /// <returns>The words.</returns>
    internal static string Percent(double scale) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale * 100, MidpointRounding.AwayFromZero):0}%");
}
