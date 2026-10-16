using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>What extraction found in one record version: replaces that record's decisions and their edges, and marks the version extracted.</summary>
/// <param name="Record">The record.</param>
/// <param name="Version">The record version the text came from.</param>
/// <param name="Decisions">Its decisions (possibly none).</param>
/// <param name="Edges">Their decides and supersedes edges.</param>
public sealed record DecisionBatch(RecordKey Record, string Version, IReadOnlyList<DecisionRow> Decisions, IReadOnlyList<GraphEdge> Edges)
{
    /// <summary>Whether the record's conflict checks are still to run: the decisions are saved but the record stays queued until a batch without this flag is saved.</summary>
    public bool Pending { get; init; }
}
