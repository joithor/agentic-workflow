using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>A checked map file, ready to write: keys normalized and migrated, decision nodes and 'quotes' edges added.</summary>
/// <param name="Repo">The repo it maps.</param>
/// <param name="Commit">Its commit, or null.</param>
/// <param name="Graph">The graph.</param>
/// <param name="Overrides">The overrides it carries.</param>
public sealed record MapImport(ContextRepoName Repo, string? Commit, GraphSnapshot Graph, IReadOnlyList<MapOverride> Overrides)
{
    /// <summary>Its counts.</summary>
    public MapCounts Counts => MapCounts.Of(Graph, Overrides.Count);
}
