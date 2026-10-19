using System.Text.Json.Serialization;
using Aw.Context.Core.Subgraph;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The subgraph around node keys (GET /sindri/context/subgraph?nodes=&amp;nodes=, or QUERY with this record as the JSON body when the keys don't fit a URL; scoping spec C14).
/// Both transports run the same service, so the same keys answer the same bytes.
/// </summary>
/// <param name="Nodes">One to 1000 node keys (20 over GET).</param>
/// <param name="Depth">How far to widen, 0 to 4, or null for 0.</param>
/// <param name="MapVersion">A retained map version, or null for the current one.</param>
/// <param name="FromUrl">True when the keys came in a URL, which holds at most 20 of them. Set by the GET route, never by a body.</param>
public sealed record GetContextSubgraphRequest(IReadOnlyList<string>? Nodes, int? Depth = null, string? MapVersion = null, [property: JsonIgnore] bool FromUrl = false) : IQuery<SubgraphView>;
