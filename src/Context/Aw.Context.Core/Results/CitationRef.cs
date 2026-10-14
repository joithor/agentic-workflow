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
}
