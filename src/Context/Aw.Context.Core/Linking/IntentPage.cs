using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>One page of a sync, committed in a single transaction together with the cursor that moves past it (spec §5.1: a cursor advances only after the links from its page commit).</summary>
/// <param name="Source">The source the page came from.</param>
/// <param name="Records">The records the page fetched (a hit whose version is unchanged is not in it, and keeps its links).</param>
/// <param name="Links">The links of those records; a record's earlier links are replaced by the page's.</param>
/// <param name="Cursor">The cursor to store after the page, or null to leave it alone.</param>
/// <param name="At">When the page was committed.</param>
public sealed record IntentPage(SourceId Source, IReadOnlyList<IntentRecord> Records, IReadOnlyList<IntentLink> Links, string? Cursor, DateTimeOffset At)
{
    /// <summary>The map version the links were computed against; stored with each record so a map change re-links it even when its upstream version didn't move. 0 (the default) records none.</summary>
    public int MapVersion { get; init; }

    /// <summary>The cleaned text of the records the page fetched, by record key, kept so a new map can link them again without a fetch (<see cref="IIntentStore.Relink"/>). A record with no entry keeps the text it has.</summary>
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
