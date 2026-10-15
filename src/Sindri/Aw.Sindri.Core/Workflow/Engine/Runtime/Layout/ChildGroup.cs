namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The children of one state that declares child or runs step.children (E-20).</summary>
/// <param name="State">The state id.</param>
/// <param name="Recipe">The child recipe.</param>
/// <param name="Done">How many children are done.</param>
/// <param name="Total">How many there are.</param>
/// <param name="Workstreams">Groups, when the state's input is a packet; otherwise empty.</param>
/// <param name="Rows">Rows used when there are no workstreams.</param>
/// <param name="More">Rows beyond the first 20.</param>
public sealed record ChildGroup(
    string State,
    string Recipe,
    int Done,
    int Total,
    IReadOnlyList<Workstream> Workstreams,
    IReadOnlyList<ChildRow> Rows,
    int More);
