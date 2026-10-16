using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>
/// What syncs wrote, independent of map versions (S-5B): records, their links, and the decisions extracted from them with the 'decides' and 'supersedes' edges.
/// <see cref="IntentOverlay"/> merges it into a graph at read.
/// </summary>
/// <param name="Records">The records.</param>
/// <param name="Links">Their links.</param>
/// <param name="Decisions">The decisions.</param>
/// <param name="DecisionEdges">The 'decides' and 'supersedes' edges between decisions and nodes or decisions.</param>
public sealed record IntentLayer(
    IReadOnlyList<IntentRecord> Records,
    IReadOnlyList<IntentLink> Links,
    IReadOnlyList<DecisionRow> Decisions,
    IReadOnlyList<GraphEdge> DecisionEdges)
{
    /// <summary>A layer with nothing in it.</summary>
    public static IntentLayer Empty { get; } = new([], [], [], []);
}
