using Aw.Context.Core.Graph;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/node?key= (context spec §8.5): one node by key, alias or name.</summary>
/// <param name="Key">The key, alias or label.</param>
public sealed record ShowContextNodeRequest(string Key) : IQuery<NodeView>;
