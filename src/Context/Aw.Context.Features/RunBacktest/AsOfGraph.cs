using Aw.Context.Core.Graph;

namespace Aw.Context.Features.RunBacktest;

/// <summary>A sample's as-of map.</summary>
/// <param name="Graph">The graph as it stood at the cut.</param>
/// <param name="Cached">True when the extraction was read from the eval cache.</param>
/// <param name="Clustered">True when the cluster source answered (the cache key records it).</param>
/// <param name="RecognizersTimedOut">True when a profile recognizer's match timed out on some file, so the map is partial and was not cached.</param>
internal sealed record AsOfGraph(GraphSnapshot Graph, bool Cached, bool Clustered, bool RecognizersTimedOut = false);
