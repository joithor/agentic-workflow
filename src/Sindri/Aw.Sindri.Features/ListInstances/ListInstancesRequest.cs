using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>
/// 'workflow list [--needs-me | --item &lt;id&gt; | --state &lt;s&gt;]' (engine spec §6.3). With no filter it lists the
/// running instances; '--item' and '--state' list ended ones too; '--query' adds a raw visibility query (for scripts).
/// </summary>
/// <param name="NeedsMe">What needs you: the Decisions and Tasks sections.</param>
/// <param name="Item">Only this item's instances.</param>
/// <param name="State">Only instances in this state ('produce', 'parked', 'done').</param>
/// <param name="Query">A visibility query on the search attributes, ANDed with the rest.</param>
public sealed record ListInstancesRequest(bool NeedsMe = false, ItemKey? Item = null, string? State = null, string? Query = null)
    : IQuery<ListInstancesResponse>;
