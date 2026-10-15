using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A gate on a state (E-20).</summary>
/// <param name="Gate">The gate id.</param>
/// <param name="On">Entry or exit.</param>
/// <param name="Threshold">The threshold.</param>
/// <param name="Description">What it checks.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GateLine(
    [property: JsonPropertyName("id")] string Gate,
    string On,
    double? Threshold,
    string Description);
