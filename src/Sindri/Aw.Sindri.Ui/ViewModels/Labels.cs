using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The app's own copy (UI spec §12.4): labels, headings and the sentences no API serves at that moment. Everything else
/// the app shows is the API's text as it is. One file, shared by every head (heads get theirs through the app, e.g.
/// <see cref="Platform.TrayView"/>). Later phases add theirs here, keyed by view name and action id.
/// </summary>
internal static class Labels
{
    // Pages and the top bar (§3.2, §3.3).

    /// <summary>The Home page.</summary>
    internal const string Home = "Home";

    /// <summary>The Queue page.</summary>
    internal const string Queue = "Queue";

    /// <summary>The Decisions page.</summary>
    internal const string Decisions = "Decisions";

    /// <summary>The Services page.</summary>
    internal const string Services = "Services";

    /// <summary>The Health page.</summary>
    internal const string Health = "Health";

    /// <summary>The Settings page.</summary>
    internal const string Settings = "Settings";

    /// <summary>The menu of views the API doesn't serve yet.</summary>
    internal const string Later = "Later";

    /// <summary>A later view (Agents, Repos, Today, Automatic work).</summary>
    internal const string Agents = "Agents";

    /// <summary>A later view.</summary>
    internal const string Repos = "Repos";

    /// <summary>A later view.</summary>
    internal const string Today = "Today";

    /// <summary>A later view.</summary>
    internal const string AutomaticWork = "Automatic work";

    /// <summary>What a view, panel or menu entry the API doesn't serve yet says (U12, §10.2).</summary>
    internal const string NotServedYet = "not served yet";

    /// <summary>The ⌘K field's placeholder (functional from U1).</summary>
    internal const string Search = "Search or run a command";

    // The states before the API answers (§4.1).

    /// <summary>First load, before /status answers.</summary>
    internal const string Connecting = "Connecting…";

    /// <summary>The control plane answers and holds the lease.</summary>
    internal const string Running = "Running";

    /// <summary>No usable control.json, or ping fails (SND-UI-001/002).</summary>
    internal const string NotRunning = "Not running";

    /// <summary>The command that starts Sindri.</summary>
    internal const string StartFix = "sindri";

    /// <summary>The health check to run.</summary>
    internal const string DoctorFix = "sindri doctor";

    /// <summary>The version-mismatch panel's sentence (API §9).</summary>
    internal const string VersionMismatch = "Sindri and the app are different versions";

    /// <summary>The version-mismatch panel's fix.</summary>
    internal const string VersionMismatchFix = "quit both and run sindri";

    /// <summary>After three failed refreshes while the process still answers ping (§4.1 Stale, DB §5).</summary>
    internal const string NotResponding = "Sindri not responding: run sindri doctor";

    /// <summary>The freshness word after 30 s with no event or ping.</summary>
    internal const string Stale = "stale";

    /// <summary>The control plane answers but holds no lease.</summary>
    internal const string NoLease = "the control plane answers but holds no lease";

    /// <summary>Not running: no control.json.</summary>
    /// <param name="location">Where the record is read from.</param>
    /// <returns>The reason.</returns>
    internal static string NoRecord(string location) => $"no endpoint record at {location}: Sindri hasn't started, or it stopped";

    /// <summary>Not running: control.json doesn't parse, or its url isn't absolute http(s).</summary>
    /// <param name="location">Where the record is read from.</param>
    /// <returns>The reason.</returns>
    internal static string RecordUnreadable(string location) => $"the endpoint record at {location} can't be read";

    /// <summary>Not running: the platform couldn't read control.json (a directory, no permission).</summary>
    /// <param name="location">Where the record is read from.</param>
    /// <param name="why">The platform's reason.</param>
    /// <returns>The reason.</returns>
    internal static string RecordUnreadable(string location, string why) => $"the endpoint record at {location} can't be read: {why}";

    /// <summary>Not running: control.json names another process or epoch than the one answering.</summary>
    /// <param name="pid">The record's pid.</param>
    /// <param name="epoch">The record's epoch.</param>
    /// <param name="answeringPid">The answering pid.</param>
    /// <param name="answeringEpoch">The answering epoch.</param>
    /// <returns>The reason.</returns>
    internal static string StaleRecord(int pid, long epoch, int answeringPid, long answeringEpoch) => string.Create(CultureInfo.InvariantCulture,
        $"the endpoint record is stale: it names pid {pid} at epoch {epoch}, but pid {answeringPid} at epoch {answeringEpoch} answers");

    /// <summary>The version-mismatch panel's versions line.</summary>
    /// <param name="sindri">The API version Sindri reports.</param>
    /// <param name="app">The API version this app speaks.</param>
    /// <returns>The line.</returns>
    internal static string Versions(int sindri, int app) => string.Create(CultureInfo.InvariantCulture, $"Sindri speaks API v{sindri}; this app speaks v{app}");

    /// <summary>"updated Ns ago" (seconds under a minute, then minutes, then hours).</summary>
    /// <param name="age">How long ago the view was last read.</param>
    /// <returns>The words.</returns>
    internal static string UpdatedAgo(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => string.Create(CultureInfo.InvariantCulture, $"updated {Math.Max(0, (int)age.TotalSeconds)}s ago"),
        < 3600 => string.Create(CultureInfo.InvariantCulture, $"updated {(int)age.TotalMinutes}m ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"updated {(int)age.TotalHours}h ago"),
    };

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

    /// <summary>A scale as a percentage ("110%").</summary>
    /// <param name="scale">The scale (1.0 is 100%).</param>
    /// <returns>The words.</returns>
    internal static string Percent(double scale) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale * 100, MidpointRounding.AwayFromZero):0}%");
}
