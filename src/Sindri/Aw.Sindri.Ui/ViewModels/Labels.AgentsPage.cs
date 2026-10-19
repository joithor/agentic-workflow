using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Agents page alone (collector spec CU-1, CU-2, CU-6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page when an answer can't be read.</summary>
    internal const string SessionsUnreadable = "The agent list can't be read from this Sindri.";

    /// <summary>The fold of sessions that ended.</summary>
    internal const string RecentlyEnded = "Recently ended";

    /// <summary>An ended session with no known way back.</summary>
    internal const string NoResumeCommand = "no resume command";

    /// <summary>The first choice of the provider filter.</summary>
    internal const string AllProviders = "All providers";

    /// <summary>The first choice of the state filter.</summary>
    internal const string AllStates = "All states";

    /// <summary>The first choice of the repo filter.</summary>
    internal const string AllRepos = "All repos";

    /// <summary>The Focus control's label (the catalog's action word).</summary>
    internal const string FocusLabel = "Focus";

    /// <summary>The CLI line Focus shows until the send is wired, when the catalog row is missing.</summary>
    internal const string FocusCliFallback = "sindri agents focus <ref>";

    /// <summary>The phase that wires the Focus send once the catalog row says it is ready.</summary>
    internal const string FocusSendPhase = "U11a-f";

    /// <summary>The recently ended fold's header with its count.</summary>
    /// <param name="count">How many ended.</param>
    /// <returns>The words.</returns>
    internal static string RecentlyEndedHeader(int count) => string.Create(CultureInfo.InvariantCulture, $"{RecentlyEnded} ({count})");

    /// <summary>A group header with its count.</summary>
    /// <param name="group">The group's name.</param>
    /// <param name="count">How many rows.</param>
    /// <returns>The words.</returns>
    internal static string GroupHeader(string group, int count) => string.Create(CultureInfo.InvariantCulture, $"{group} ({count})");

    /// <summary>A launched session's line.</summary>
    /// <param name="launchId">The launch id as served.</param>
    /// <returns>The words.</returns>
    internal static string LaunchLine(string launchId) => "launch " + launchId;

    /// <summary>A lane's line: "meta · sindri/ideas".</summary>
    /// <param name="team">The team.</param>
    /// <param name="lane">The lane.</param>
    /// <returns>The words.</returns>
    internal static string LaneLine(string team, string lane) => "meta · " + team + "/" + lane;

    /// <summary>What a waiting session waits on: "approval · Bash".</summary>
    /// <param name="reason">The served reason.</param>
    /// <param name="tool">The served tool, or null.</param>
    /// <returns>The words.</returns>
    internal static string AskingDetail(string reason, string? tool) => string.IsNullOrEmpty(tool) ? reason : reason + " · " + tool;

    /// <summary>An ended row's time.</summary>
    /// <param name="age">How long ago it ended.</param>
    /// <returns>The words.</returns>
    internal static string EndedAgo(TimeSpan age) => "ended " + Age(age) + " ago";

    /// <summary>A session row's accessible name: provider, title, state, what it asks, host.</summary>
    /// <param name="provider">The provider's label.</param>
    /// <param name="title">The title or its fallback.</param>
    /// <param name="state">The state's word.</param>
    /// <param name="detail">What it asks, or empty.</param>
    /// <param name="host">The host's label.</param>
    /// <returns>The words.</returns>
    internal static string SessionName(string provider, string title, string state, string detail, string host) =>
        string.Join(", ", new[] { provider, title, state, detail, host }.Where(p => p.Length > 0));

    /// <summary>The Step an engine launch started a session for: "on ITM-…/step/1 · build", with "· attempt 2" above the first attempt.</summary>
    /// <param name="instance">The instance address.</param>
    /// <param name="state">The Step's state, or null.</param>
    /// <param name="attempt">The attempt, or null.</param>
    /// <returns>The words.</returns>
    internal static string SessionStep(string instance, string? state, int? attempt) =>
        string.Join(" · ", new[] { "on " + instance, state ?? string.Empty, attempt is > 1 ? "attempt " + attempt.Value.ToString(CultureInfo.InvariantCulture) : string.Empty }.Where(p => p.Length > 0));
}
