using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A packet workstream and its children (E-20).</summary>
/// <param name="Key">The workstream's key (wire name 'id').</param>
/// <param name="Label">The label.</param>
/// <param name="Rows">The children in it.</param>
public sealed record Workstream(
    [property: JsonPropertyName("id")] string Key,
    string Label,
    IReadOnlyList<ChildRow> Rows);
