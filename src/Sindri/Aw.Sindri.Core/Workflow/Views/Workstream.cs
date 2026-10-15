using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A packet workstream (E-20).</summary>
/// <param name="Stream">The id.</param>
/// <param name="Label">The label.</param>
/// <param name="Rows">Its children.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Workstream(
    [property: JsonPropertyName("id")] string Stream,
    string Label,
    IReadOnlyList<ChildRow> Rows);
