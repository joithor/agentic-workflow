namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>What one babysitter instance's transitions add up to.</summary>
internal sealed class BabysitterRun
{
    /// <summary>Seconds from the start to the first entry of ready, or null when it never got there.</summary>
    public double? ReadyAfter { get; init; }

    /// <summary>Seconds from the start to the entry of merged, or null.</summary>
    public double? MergeAfter { get; init; }

    /// <summary>The tokens the instance spent (the ledger carries a running total), or null when no row says.</summary>
    public int? Tokens { get; init; }

    /// <summary>What the owner did at stop 3: the count of transitions out of ready for each verb.</summary>
    public OwnerActionStats Owner { get; init; } = new(Merge: 0, Rework: 0, Comments: 0, ResyncDocs: 0, Check: 0);

    /// <summary>The CI fixes.</summary>
    public CiFixStats CiFix { get; init; } = new(Attempts: 0, Succeeded: 0, StillRed: 0, Rate: null);

    /// <summary>The rechecks.</summary>
    public RecheckStats Rechecks { get; init; } = new(Runs: 0, Resolved: 0, Unresolved: 0);

    /// <summary>Entries into the docs-merging state.</summary>
    public int DocsWithMerge { get; init; }

    /// <summary>Entries into the docs-merge desk.</summary>
    public int DocsOwnerDesk { get; init; }
}
