using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /workflow/instances/{id}/facts/{key}: the body behind one fact row.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Body">Body, as served.</param>
/// <param name="Truncated">Truncated, as served.</param>
internal sealed record FactView(
    string Key,
    FactType Type,
    JsonElement Body,
    bool Truncated);
