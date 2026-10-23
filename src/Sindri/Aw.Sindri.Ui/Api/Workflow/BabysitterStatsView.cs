namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The pull request babysitter's own figures in a recipe's stats (GET /workflow/recipes/{id}/stats, only for workflow.pr-babysitter; api #778).</summary>
/// <param name="Pulls">Pull requests it watched in the window.</param>
/// <param name="SinceStartToReadySeconds">Time from start until a pull request was ready for you.</param>
/// <param name="SinceStartToMergeSeconds">Time from start until it merged.</param>
/// <param name="TokensPerPull">Tokens used per pull request.</param>
/// <param name="Comments">What happened to review comments.</param>
/// <param name="CiFix">CI repair attempts.</param>
/// <param name="Rechecks">Rechecks of a red check.</param>
/// <param name="OwnerActionsAtReady">What you did when a pull request was ready.</param>
/// <param name="Docs">How the documentation updates went.</param>
/// <param name="RecordErrors">Merge records that couldn't be read.</param>
internal sealed record BabysitterStatsView(
    int Pulls,
    StatsSpreadView SinceStartToReadySeconds,
    StatsSpreadView SinceStartToMergeSeconds,
    StatsSpreadView TokensPerPull,
    BabysitterCommentsView Comments,
    BabysitterCiFixView CiFix,
    BabysitterRechecksView Rechecks,
    BabysitterOwnerActionsView OwnerActionsAtReady,
    BabysitterDocsView Docs,
    int RecordErrors);

/// <summary>A typical and a heavy figure; each null when there were not enough runs.</summary>
/// <param name="Median">The typical value.</param>
/// <param name="P90">The value nine in ten stay under.</param>
internal sealed record StatsSpreadView(long? Median, long? P90);

/// <summary>What happened to review comments.</summary>
/// <param name="Total">All comments seen.</param>
/// <param name="Held">Comments held for you.</param>
/// <param name="WithoutOwner">Comments handled without you.</param>
/// <param name="ByType">One row per comment type, as served.</param>
internal sealed record BabysitterCommentsView(int Total, int Held, int WithoutOwner, IReadOnlyList<BabysitterCommentTypeView> ByType);

/// <summary>What was done with comments of one type.</summary>
/// <param name="Type">The type, as served (a type this build doesn't know shows raw).</param>
/// <param name="Fixed">Fixed in code.</param>
/// <param name="Replied">Answered with a reply.</param>
/// <param name="Reacted">Answered with a reaction.</param>
/// <param name="Held">Held for you.</param>
/// <param name="Skipped">Skipped.</param>
internal sealed record BabysitterCommentTypeView(string Type, int Fixed, int Replied, int Reacted, int Held, int Skipped);

/// <summary>CI repair attempts.</summary>
/// <param name="Attempts">Attempts made.</param>
/// <param name="Succeeded">Attempts that turned CI green.</param>
/// <param name="StillRed">Attempts that left CI red.</param>
/// <param name="Rate">The share that succeeded (0 to 1); null when there were no attempts.</param>
internal sealed record BabysitterCiFixView(int Attempts, int Succeeded, int StillRed, double? Rate);

/// <summary>Rechecks of a red check.</summary>
/// <param name="Runs">Rechecks run.</param>
/// <param name="Resolved">Rechecks that cleared it.</param>
/// <param name="Unresolved">Rechecks that did not.</param>
internal sealed record BabysitterRechecksView(int Runs, int Resolved, int Unresolved);

/// <summary>What you did at the moment a pull request was ready.</summary>
/// <param name="Merge">Merged it.</param>
/// <param name="Rework">Sent it back for rework.</param>
/// <param name="Comments">Dealt with comments.</param>
/// <param name="ResyncDocs">Asked for the docs to be redone.</param>
/// <param name="Check">Ran a check.</param>
internal sealed record BabysitterOwnerActionsView(int Merge, int Rework, int Comments, int ResyncDocs, int Check);

/// <summary>How the documentation updates went.</summary>
/// <param name="WithMerge">Pull requests whose docs merged with them.</param>
/// <param name="OwnerDesk">Docs that waited for you.</param>
/// <param name="ByOutcome">Counts by outcome, as served (merged, pending, none, doneByHand, left).</param>
internal sealed record BabysitterDocsView(int WithMerge, int OwnerDesk, IReadOnlyList<BabysitterDocOutcomeView> ByOutcome);

/// <summary>Docs updates that ended one way.</summary>
/// <param name="Reason">The outcome, as served.</param>
/// <param name="N">How many.</param>
internal sealed record BabysitterDocOutcomeView(string Reason, int N);
