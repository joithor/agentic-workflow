using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/node.</summary>
/// <param name="MapVersion">MapVersion, as served.</param>
/// <param name="Node">Node, as served.</param>
/// <param name="Aliases">Aliases, as served.</param>
/// <param name="Out">Out, as served.</param>
/// <param name="In">In, as served.</param>
internal sealed record NodeView(
    string MapVersion,
    NodeInfo Node,
    IReadOnlyList<JsonElement> Aliases,
    IReadOnlyList<NodeEdge> Out,
    IReadOnlyList<NodeEdge> In);
