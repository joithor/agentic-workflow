using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>The map a caller may read: the version, and its graph with overrides applied and a session's scope cut.</summary>
/// <param name="Snapshot">The version as stored.</param>
/// <param name="Graph">The graph the caller sees.</param>
public sealed record VisibleMap(MapSnapshot Snapshot, GraphSnapshot Graph)
{
    /// <summary>The checked decision pairs whose two decisions the caller can see (S-5B).</summary>
    public IReadOnlyList<Linking.DecisionConflict> Conflicts { get; init; } = [];
}
