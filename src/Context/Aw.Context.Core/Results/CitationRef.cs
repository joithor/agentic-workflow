using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>One source's reference under a citation.</summary>
/// <param name="Source">The source.</param>
/// <param name="Id">The record's id in it.</param>
/// <param name="Version">The cited version.</param>
public sealed record CitationRef(SourceId Source, ExternalId Id, SourceVersion Version)
{
    /// <summary>The source's current version when the citation is stale, else null.</summary>
    public SourceVersion? Current { get; init; }

    /// <summary>When the record was last fetched: the intent store's 'fetched_at' for a synced record, the live fetch's time for one read live, the map version's creation for a map-authored one.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? FetchedAt { get; init; }
}
