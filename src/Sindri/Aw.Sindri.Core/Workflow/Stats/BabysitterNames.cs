namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// The states and verbs of 'workflow.pr-babysitter' that the babysitter stats key on (PR babysitter spec API19), in one place. A test loads the shipped recipe and fails when one is gone, so a
/// rename of a state or a command cannot silently zero a figure.
/// </summary>
public static class BabysitterNames
{
    /// <summary>The recipe the stats are about.</summary>
    public const string Recipe = "workflow.pr-babysitter";

    /// <summary>Stop 3: the pull request is ready for the owner.</summary>
    public const string Ready = "ready";

    /// <summary>The terminal state of a merged pull request.</summary>
    public const string Merged = "merged";

    /// <summary>The state that reads a snapshot of the pull request.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>The CI-fix agent state.</summary>
    public const string CiFix = "ci-fix";

    /// <summary>The proof recheck state.</summary>
    public const string Recheck = "recheck";

    /// <summary>The desk a failed recheck stops at.</summary>
    public const string RecheckOwner = "recheck-owner";

    /// <summary>Merging the pull request and, with it, its docs pull request.</summary>
    public const string DocsMerging = "docs-merging";

    /// <summary>The desk that decides a docs pull request merged or left elsewhere.</summary>
    public const string DocsMergeOwner = "docs-merge-owner";

    /// <summary>Where the owner's 'merge' takes the instance from <see cref="Ready"/>.</summary>
    public const string MergingFromReady = "merging";

    /// <summary>Where the owner's 'rework' takes it.</summary>
    public const string ReworkNotes = "rework-notes";

    /// <summary>Where the owner's 'comments' takes it.</summary>
    public const string HeldNext = "held-next";

    /// <summary>Where the owner's 'resync-docs' takes it.</summary>
    public const string Docs = "docs";

    /// <summary>The merge record's row kind for a comment.</summary>
    public const string CommentRow = "comment";

    /// <summary>The merge record's row kind for a docs merge.</summary>
    public const string DocsMergeRow = "docsMerge";

    /// <summary>The comment outcome that waits for the owner.</summary>
    public const string HeldOutcome = "held";

    /// <summary>The verbs the owner sends at stop 3 and the states each leads to (merge, rework, comments, resync-docs, check).</summary>
    public static IReadOnlyDictionary<string, string> OwnerVerbs { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["merge"] = MergingFromReady,
        ["rework"] = ReworkNotes,
        ["comments"] = HeldNext,
        ["resync-docs"] = Docs,
        ["check"] = Snapshot,
    };
}
