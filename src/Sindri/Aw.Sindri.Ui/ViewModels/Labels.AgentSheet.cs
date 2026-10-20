using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the session sheet's labels and sentences (collector spec CU-3). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The label of the session's working directory.</summary>
    internal const string SheetWorkingDirectory = "Working directory";

    /// <summary>The label of the session's worktree.</summary>
    internal const string SheetWorktree = "Worktree";

    /// <summary>The label of the session's lane.</summary>
    internal const string SheetLane = "Lane";

    /// <summary>The label of the launch that started the session.</summary>
    internal const string SheetLaunch = "Agent session";

    /// <summary>The label of the workflow instance an engine launch started the session for.</summary>
    internal const string SheetInstance = "Run";

    /// <summary>The label of that instance's Step state.</summary>
    internal const string SheetStep = "Step";

    /// <summary>The label of the attempt of that Step.</summary>
    internal const string SheetAttempt = "Attempt";

    /// <summary>The visible words of the action that opens the Step's frame.</summary>
    internal const string OpenStepFrame = "Open the run";

    /// <summary>The label of the time the process started.</summary>
    internal const string SheetStarted = "Started";

    /// <summary>The label of the time the state began.</summary>
    internal const string SheetSince = "In this status since";

    /// <summary>The label of where the state came from.</summary>
    internal const string SheetStateSource = "How this was detected";

    /// <summary>The label of where the session runs.</summary>
    internal const string SheetHost = "Host";

    /// <summary>The label of the host's terminal.</summary>
    internal const string SheetTerminal = "Terminal";

    /// <summary>The label of the tmux socket's kind.</summary>
    internal const string SheetSocketKind = "Socket kind";

    /// <summary>The label of the tmux session's name.</summary>
    internal const string SheetTmuxSession = "Tmux session";

    /// <summary>The label of the tmux pane.</summary>
    internal const string SheetPane = "Pane";

    /// <summary>The label of the tmux socket's path.</summary>
    internal const string SheetSocket = "Socket";

    /// <summary>The label of the tmux clients attached.</summary>
    internal const string SheetAttachedClients = "Attached clients";

    /// <summary>The heading of the status history.</summary>
    internal const string SheetHistory = "History";

    /// <summary>The mark on the current state in the history.</summary>
    internal const string SheetNow = "now";

    /// <summary>The word after a chip whose state is a guess.</summary>
    internal const string SheetEstimated = "estimated";

    /// <summary>The disclosure that holds the session's technical fields, its history and Watch the agent. Closed by default.</summary>
    internal const string SheetDetails = "Details";

    /// <summary>The sheet's sentence when the answer can't be read.</summary>
    internal const string SessionUnreadable = "This session's answer can't be read by this build of the app.";

    /// <summary>A time in the sheet: the day and the second, in UTC.</summary>
    /// <param name="at">The time.</param>
    /// <returns>The words.</returns>
    internal static string SheetTime(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>The stale note: the last read failed and the fields are the last read's.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string SessionStale(Error error) => "Couldn't read the session just now. " + InstanceRefused(error);

    /// <summary>The accessible name of the action that opens the Step's frame.</summary>
    /// <param name="state">The step's state, or null.</param>
    /// <returns>The words.</returns>
    internal static string OpenStepFrameName(string? state) => state is { Length: > 0 } ? $"{OpenStepFrame} at {state}" : OpenStepFrame;
}
