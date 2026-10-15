using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Ranking;

/// <summary>One real-world record after dedup and fusion.</summary>
/// <param name="Key">Its canonical key.</param>
/// <param name="Score">Its reciprocal-rank-fusion score.</param>
/// <param name="Hits">Its best hit per source, best first (lowest per-source rank, ties by source id).</param>
/// <param name="Why">Each source's rank: 'rrf:repo-docs#1'.</param>
public sealed record FusedItem(RecordKey Key, double Score, IReadOnlyList<SourceHit> Hits, IReadOnlyList<string> Why)
{
    /// <summary>The record's other names (<see cref="SourceHit.Aliases"/>) from every hit of it, best hit first, each once.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
