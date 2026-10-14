using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Ranking;

/// <summary>One source's hits for a query, across its sub-queries.</summary>
/// <param name="Source">The source.</param>
/// <param name="Hits">Its hits; a record may appear more than once (several sub-queries, several chunks).</param>
public sealed record SourceAnswer(SourceId Source, IReadOnlyList<SourceHit> Hits);
