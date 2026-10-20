using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The children of one state (E-20).</summary>
/// <param name="State">The state.</param>
/// <param name="Recipe">The recipe the children run (a real workflow.* id).</param>
/// <param name="DisplayName">The children's recipe name, shown instead of the id (setup-once spec §7.7).</param>
/// <param name="Done">Done children.</param>
/// <param name="Total">All children.</param>
/// <param name="Workstreams">Packet workstreams.</param>
/// <param name="Rows">Rows when there are no workstreams.</param>
/// <param name="More">Rows beyond the first 20.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildGroup(
    string State,
    RecipeId Recipe,
    string DisplayName,
    int Done,
    int Total,
    IReadOnlyList<Workstream> Workstreams,
    IReadOnlyList<ChildRow> Rows,
    int More);
