using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A workstream inside a child group.</summary>
/// <param name="Stream">Stream, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Rows">Rows, as served.</param>
internal sealed record Workstream(
    [property: JsonPropertyName("id")] string Stream,
    string Label,
    IReadOnlyList<ChildRow> Rows);
