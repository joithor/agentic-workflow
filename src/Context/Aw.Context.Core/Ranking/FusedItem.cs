using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Ranking;

/// <summary>One real-world record after dedup and fusion.</summary>
/// <param name="Key">Its canonical key.</param>
/// <param name="Score">Its reciprocal-rank-fusion score.</param>
/// <param name="Hits">Its best hit per source, in source order.</param>
/// <param name="Why">Each source's rank: 'rrf:repo-docs#1'.</param>
public sealed record FusedItem(RecordKey Key, double Score, IReadOnlyList<SourceHit> Hits, IReadOnlyList<string> Why);
