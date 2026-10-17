using Aw.Context.Core.Graph;

namespace Aw.Context.Features.RunBacktest;

/// <summary>A sample's as-of map.</summary>
/// <param name="Graph">The graph as it stood at the cut.</param>
/// <param name="Cached">True when the extraction was read from the eval cache.</param>
/// <param name="Clustered">True when the cluster source answered (the cache key records it).</param>
internal sealed record AsOfGraph(GraphSnapshot Graph, bool Cached, bool Clustered);
