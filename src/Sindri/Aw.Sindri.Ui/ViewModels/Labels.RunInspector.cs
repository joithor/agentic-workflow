using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Workflows run drawer (F3 workflows spec §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The drawer's group name for screen readers.</summary>
    internal const string RunDetails = "Run details";

    /// <summary>The drawer's close button.</summary>
    internal const string RunClose = "Close";

    /// <summary>The drawer's previous-run button.</summary>
    internal const string RunPrevious = "Previous run";

    /// <summary>The drawer's next-run button.</summary>
    internal const string RunNext = "Next run";

    /// <summary>The button that copies the run's reference.</summary>
    internal const string RunCopyReference = "Copy reference";

    /// <summary>The drawer's primary action: go to the run.</summary>
    internal const string RunOpen = "Open run";

    /// <summary>The drawer's take-over action (the catalog's label).</summary>
    internal const string RunTakeOver = "Take over";

    /// <summary>The drawer's hand-back action.</summary>
    internal const string RunHandBack = "Hand back";

    /// <summary>The drawer's cancel action (the catalog's label).</summary>
    internal const string RunCancel = "Cancel run";

    /// <summary>The stage list's caption.</summary>
    internal const string RunStagesEyebrow = "STAGES";

    /// <summary>The needs-you callout's body when the run serves no text.</summary>
    internal const string RunNeedsBody = "Open the request to read it, then decide.";

    /// <summary>The needs-you callout's primary button.</summary>
    internal const string RunReviewDecide = "Review and decide";

    /// <summary>The needs-you callout's terminal chip.</summary>
    internal const string RunTerminal = "Or run in a terminal";

    /// <summary>The took-over callout's title.</summary>
    internal const string RunTookTitle = "You took over";

    /// <summary>The failed callout's body.</summary>
    internal const string RunFailedBody = "This run ended without finishing. Open the run to see why.";

    /// <summary>The words after a copy.</summary>
    internal const string RunCopied = "Copied";

    /// <summary>The drawer's position among the runs.</summary>
    /// <param name="n">The run's place, from 1.</param>
    /// <param name="of">How many runs.</param>
    /// <returns>"3 of 12".</returns>
    internal static string RunPosition(int n, int of) => string.Create(CultureInfo.InvariantCulture, $"{n} of {of}");

    /// <summary>The stage list's progress.</summary>
    /// <param name="done">Stages done.</param>
    /// <param name="total">Stages in all.</param>
    /// <returns>"2 of 5 done".</returns>
    internal static string RunStagesDone(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done} of {total} done");

    /// <summary>How long the run has been in a stage.</summary>
    /// <param name="stage">The stage's name.</param>
    /// <param name="time">The time, as the rows show it.</param>
    /// <returns>"Design for 1h 30m".</returns>
    internal static string RunStageFor(string stage, string time) => stage + " for " + time;

    /// <summary>How long ago a run ended.</summary>
    /// <param name="time">The time, as the rows show it.</param>
    /// <returns>"Ended 5h 20m ago".</returns>
    internal static string RunEnded(string time) => "Ended " + time + " ago";

    /// <summary>The needs-you callout's title.</summary>
    /// <param name="what">What the run waits on.</param>
    /// <returns>"Needs you: Approve the design".</returns>
    internal static string RunNeedsTitle(string what) => "Needs you: " + what;

    /// <summary>The drawer's footer.</summary>
    /// <param name="reference">The run's reference.</param>
    /// <returns>The sentence.</returns>
    internal static string RunReferenceNote(string reference) => "Reference " + reference + " is for pasting into a terminal.";
}
