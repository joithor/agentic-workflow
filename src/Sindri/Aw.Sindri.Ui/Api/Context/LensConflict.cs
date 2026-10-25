using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A conflict between decisions.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Node">Node, as served.</param>
/// <param name="Decision">Decision, as served.</param>
/// <param name="Other">Other, as served.</param>
/// <param name="Summary">Summary, as served.</param>
/// <param name="Intent">Intent, as served.</param>
/// <param name="Behavior">Behavior, as served.</param>
internal sealed record LensConflict(
    string Kind,
    string Node,
    string? Decision,
    string? Other,
    string Summary,
    IReadOnlyList<JsonElement> Intent,
    IReadOnlyList<JsonElement> Behavior);
