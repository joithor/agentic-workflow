using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the pull request babysitter's figures on the recipe sheet (api #778). Plain words, sentence case, no em dashes.</summary>
internal static partial class Labels
{
    /// <summary>The heading over the babysitter's figures.</summary>
    internal const string BabysitterHeading = "Babysitter";

    /// <summary>Said for a figure that needs more runs.</summary>
    internal const string BabysitterNotEnough = "Not enough runs yet";

    /// <summary>The heading over the pull request figures.</summary>
    internal const string BabysitterPullsHeading = "Pull requests";

    /// <summary>The heading over the comment figures.</summary>
    internal const string BabysitterCommentsHeading = "Review comments";

    /// <summary>The heading over the CI fix figures.</summary>
    internal const string BabysitterCiHeading = "CI fixes";

    /// <summary>The heading over the recheck figures.</summary>
    internal const string BabysitterRechecksHeading = "Rechecks";

    /// <summary>The heading over what the person did at ready.</summary>
    internal const string BabysitterOwnerHeading = "What you did when a pull request was ready";

    /// <summary>The heading over the docs figures.</summary>
    internal const string BabysitterDocsHeading = "Docs";

    /// <summary>Pull requests watched.</summary>
    internal const string BabysitterPulls = "Watched";

    /// <summary>Time until ready.</summary>
    internal const string BabysitterToReady = "Time to ready";

    /// <summary>Time until merged.</summary>
    internal const string BabysitterToMerge = "Time to merge";

    /// <summary>Tokens per pull request.</summary>
    internal const string BabysitterTokens = "Tokens per pull request";

    /// <summary>All comments.</summary>
    internal const string BabysitterCommentsTotal = "Comments seen";

    /// <summary>Comments held for the person.</summary>
    internal const string BabysitterCommentsHeld = "Held for you";

    /// <summary>Comments handled without the person.</summary>
    internal const string BabysitterCommentsWithout = "Handled without you";

    /// <summary>CI attempts.</summary>
    internal const string BabysitterCiAttempts = "Attempts";

    /// <summary>CI attempts that turned CI green.</summary>
    internal const string BabysitterCiSucceeded = "Turned CI green";

    /// <summary>CI attempts that left CI red.</summary>
    internal const string BabysitterCiStillRed = "Still red";

    /// <summary>The CI fix success rate.</summary>
    internal const string BabysitterCiRate = "Success rate";

    /// <summary>Rechecks run.</summary>
    internal const string BabysitterRechecksRuns = "Rechecks run";

    /// <summary>Rechecks that cleared the check.</summary>
    internal const string BabysitterRechecksResolved = "Cleared it";

    /// <summary>Rechecks that did not.</summary>
    internal const string BabysitterRechecksUnresolved = "Did not clear it";

    /// <summary>What the person did: merged.</summary>
    internal const string BabysitterOwnerMerge = "Merged";

    /// <summary>What the person did: sent back for rework.</summary>
    internal const string BabysitterOwnerRework = "Sent back for rework";

    /// <summary>What the person did: dealt with comments.</summary>
    internal const string BabysitterOwnerComments = "Dealt with comments";

    /// <summary>What the person did: asked for the docs again.</summary>
    internal const string BabysitterOwnerResync = "Asked for the docs again";

    /// <summary>What the person did: ran a check.</summary>
    internal const string BabysitterOwnerCheck = "Ran a check";

    /// <summary>Pull requests whose docs merged with them.</summary>
    internal const string BabysitterDocsWithMerge = "Merged with the pull request";

    /// <summary>Docs that waited for the person.</summary>
    internal const string BabysitterDocsOwnerDesk = "Waited for you";

    /// <summary>A median and a p90 in words: "typical 1h 30m, most under 4h".</summary>
    /// <param name="median">The typical value, or null.</param>
    /// <param name="p90">The value nine in ten stay under, or null.</param>
    /// <param name="format">How one value reads.</param>
    /// <returns>The words, or <see cref="BabysitterNotEnough"/> when either is missing.</returns>
    internal static string BabysitterSpread(long? median, long? p90, Func<long, string> format) =>
        median is { } typical && p90 is { } heavy ? $"typical {format(typical)}, most under {format(heavy)}" : BabysitterNotEnough;

    /// <summary>One comment type's row in words: "fixed 3, replied 1"; <see cref="StatNone"/> when nothing was done.</summary>
    /// <param name="parts">The non-zero parts, already worded.</param>
    /// <returns>The row.</returns>
    internal static string BabysitterCommentRow(IReadOnlyList<string> parts) => parts.Count == 0 ? StatNone : string.Join(", ", parts);

    /// <summary>The docs outcome in words; an outcome this build doesn't know shows as served.</summary>
    /// <param name="reason">The outcome.</param>
    /// <returns>The words.</returns>
    internal static string BabysitterDocsOutcome(string reason) => reason switch
    {
        "merged" => "Docs merged",
        "pending" => "Docs still pending",
        "none" => "No docs needed",
        "doneByHand" => "Done by hand",
        "left" => "Left undone",
        _ => reason,
    };

    /// <summary>The line when merge records couldn't be read.</summary>
    /// <param name="n">How many.</param>
    /// <returns>The sentence.</returns>
    internal static string BabysitterRecordErrors(int n) =>
        string.Create(CultureInfo.InvariantCulture, $"{n:N0} merge {(n == 1 ? "record" : "records")} couldn't be read");
}
