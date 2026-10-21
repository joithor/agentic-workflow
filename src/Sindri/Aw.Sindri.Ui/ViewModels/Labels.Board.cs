namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Workflows board, list and run drawer (F3 workflows spec §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>A run that is working.</summary>
    internal const string WorkflowsStatusRunning = "Running";

    /// <summary>A run that waits on a choice or a decision from the user.</summary>
    internal const string WorkflowsStatusWaitingForYou = "Waiting for you";

    /// <summary>A run that stopped itself (used its tries, ran out of allowance, timed out) and needs the user.</summary>
    internal const string WorkflowsStatusPaused = "Paused, needs you";

    /// <summary>A run the user took over.</summary>
    internal const string WorkflowsStatusTookOver = "You took over";

    /// <summary>A run that has not started.</summary>
    internal const string WorkflowsStatusNotStarted = "Not started";

    /// <summary>A run that finished.</summary>
    internal const string WorkflowsStatusDone = "Done";

    /// <summary>A run that failed.</summary>
    internal const string WorkflowsStatusFailed = "Failed";

    /// <summary>A run that ended without resolving what it was asked (the attention tone).</summary>
    internal const string WorkflowsStatusDidntWorkOut = "Didn't work out";

    /// <summary>A run the user cancelled.</summary>
    internal const string WorkflowsStatusCancelled = "Cancelled";

    /// <summary>The board's first lane (a stopgap table until the engine serves stage names).</summary>
    internal const string WorkflowsLaneScoping = "Scoping";

    /// <summary>The board's second lane.</summary>
    internal const string WorkflowsLaneDesign = "Design";

    /// <summary>The board's third lane.</summary>
    internal const string WorkflowsLanePlan = "Plan";

    /// <summary>The board's fourth lane.</summary>
    internal const string WorkflowsLaneBuild = "Build and prove";

    /// <summary>The board's fifth lane.</summary>
    internal const string WorkflowsLaneMerge = "Merge";

    /// <summary>The board's last lane.</summary>
    internal const string WorkflowsLaneDone = "Done";

    /// <summary>An empty lane's first line.</summary>
    internal const string WorkflowsLaneEmpty = "Nothing here.";

    /// <summary>An empty lane's second line.</summary>
    internal const string WorkflowsLaneEmptyBody = "No runs in this stage.";

    /// <summary>What an empty Merge lane teaches.</summary>
    internal const string WorkflowsLaneMergeEmpty = "Runs wait here for CI and docs, then for your approval.";

    /// <summary>An empty Done lane.</summary>
    internal const string WorkflowsLaneDoneEmpty = "No finished runs yet.";
}
