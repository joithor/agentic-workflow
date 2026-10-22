namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the classifier reads for one repo; the Host adapter fills it from the index's stored heads, the live head, the service state and the index:&lt;repo&gt; jobs.</summary>
/// <param name="IndexCommit">The commit the stored index names (ICodeIndex.Heads); null when there is no readable index (missing or damaged).</param>
/// <param name="HoldHead">The commit the held map's build read for this repo.</param>
/// <param name="HoldOnDefaultBranch">Whether that commit was on the repo's default branch (the index never builds from a feature branch).</param>
/// <param name="LiveHead">The repo's head now; null when unknown.</param>
/// <param name="InScope">Whether the repo is in the code index service's scope.</param>
/// <param name="ServiceActive">Whether the code-index-main-move service exists and is not paused.</param>
/// <param name="BuildRunning">Whether a BuildCodeIndex job of the repo's key is running.</param>
/// <param name="NeverBuilt">No index has ever been published for the repo (no file, and no succeeded job of the key that published a commit).</param>
/// <param name="FinishedNewestFirst">The key's newest ended jobs, newest first.</param>
public sealed record IndexOutlookInput(
    string? IndexCommit,
    string HoldHead,
    bool HoldOnDefaultBranch,
    string? LiveHead,
    bool InScope,
    bool ServiceActive,
    bool BuildRunning,
    bool NeverBuilt,
    IReadOnlyList<IndexAttempt> FinishedNewestFirst);
