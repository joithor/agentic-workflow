using Aw.Context.Core.Graph;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/path?from=&amp;to= (context spec §8.5): the shortest path between two nodes.</summary>
/// <param name="From">A key, alias or label.</param>
/// <param name="To">A key, alias or label.</param>
public sealed record FindContextPathRequest(string From, string To) : IQuery<PathView>;
