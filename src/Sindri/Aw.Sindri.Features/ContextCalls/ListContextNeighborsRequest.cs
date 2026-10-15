using Aw.Context.Core.Graph;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/neighbors?key=&amp;depth= (context spec §8.5): the nodes near one node.</summary>
/// <param name="Key">The key, alias or label.</param>
/// <param name="Depth">0 to 4 as sent (anything else is SND-CONTEXT-006), or null for 1.</param>
public sealed record ListContextNeighborsRequest(string Key, string? Depth = null) : IQuery<NeighborsView>;
