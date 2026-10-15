namespace Aw.Sindri.Host.Commands;

/// <summary>What a sent request counts as in the closing summary.</summary>
internal enum ReviewOutcome
{
    /// <summary>Accepted (one item, or the whole deterministic backlog).</summary>
    Accepted,

    /// <summary>Rejected.</summary>
    Rejected,

    /// <summary>Renamed or moved: the item stays undecided.</summary>
    Edited,

    /// <summary>An orphaned override dropped or repointed.</summary>
    Dropped,
}
