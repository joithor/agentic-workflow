namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A packet workstream and its children (E-20).</summary>
/// <param name="Id">The workstream id.</param>
/// <param name="Label">The label.</param>
/// <param name="Rows">The children in it.</param>
public sealed record Workstream(
    string Id,
    string Label,
    IReadOnlyList<ChildRow> Rows);
