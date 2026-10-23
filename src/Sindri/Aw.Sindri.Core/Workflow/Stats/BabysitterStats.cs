namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// The babysitter's own figures (PR babysitter spec API19) for the recipe stats window: what PB-P2 reads to tune the caps and Q5, Q9 and Q12. The times run from when the babysitter started,
/// which for a pull request opened by hand is when it was opened, and for one a line opened is a little after; both name it "since start". The comment figures come from the merge records
/// ('babysit.jsonl'); the rest from the ledger's transitions.
/// </summary>
/// <param name="Pulls">The babysitter instances started in the window.</param>
/// <param name="SinceStartToReadySeconds">Seconds from the start to the first ready-to-merge, over the instances that got there.</param>
/// <param name="SinceStartToMergeSeconds">Seconds from the start to the merge, over the merged instances.</param>
/// <param name="TokensPerPull">Tokens an instance spent.</param>
/// <param name="Comments">The comments by type and outcome.</param>
/// <param name="CiFix">CI fixes.</param>
/// <param name="Rechecks">Proof rechecks.</param>
/// <param name="OwnerActionsAtReady">What the owner did at stop 3.</param>
/// <param name="Docs">Docs pull requests merged or left.</param>
/// <param name="RecordErrors">Merge-record lines that could not be read (not JSON, or no kind); they are left out of the figures.</param>
public sealed record BabysitterStats(
    int Pulls,
    BabysitterSpread SinceStartToReadySeconds,
    BabysitterSpread SinceStartToMergeSeconds,
    BabysitterSpread TokensPerPull,
    BabysitterCommentStats Comments,
    CiFixStats CiFix,
    RecheckStats Rechecks,
    OwnerActionStats OwnerActionsAtReady,
    BabysitterDocsStats Docs,
    int RecordErrors);
