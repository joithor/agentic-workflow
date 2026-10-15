namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The children of one state, one recipe.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Recipe">Recipe, as served.</param>
/// <param name="Done">Done, as served.</param>
/// <param name="Total">Total, as served.</param>
/// <param name="Workstreams">Workstreams, as served.</param>
/// <param name="Rows">Rows, as served.</param>
/// <param name="More">More, as served.</param>
internal sealed record ChildGroup(
    string State,
    string Recipe,
    int Done,
    int Total,
    IReadOnlyList<Workstream> Workstreams,
    IReadOnlyList<ChildRow> Rows,
    int More);
