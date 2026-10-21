using Aw.Context.Core.Engine;
using Aw.Context.Core.Subgraph;

namespace Aw.Context.Features.GetSubgraph;

/// <summary>A subgraph around a set of node keys (schema 1).</summary>
/// <param name="Query">The keys, the depth and which map.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetSubgraphRequest(SubgraphQuery Query, ContextScope Scope);
