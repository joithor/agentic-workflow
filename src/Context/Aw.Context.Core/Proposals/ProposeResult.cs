using System.Text.Json.Serialization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>What proposing wrote; 'current' is never touched.</summary>
/// <param name="Draft">The draft the rows went to (opened as a copy of current when there was none).</param>
/// <param name="Added">New 'propose' rows.</param>
/// <param name="Existing">Surfaces already in the map or already proposed (idempotent: no row).</param>
/// <param name="Rejected">Surfaces a person rejected earlier (never re-proposed: no row).</param>
/// <param name="Rows">The ids of the 'propose' rows this run has in the draft for the call's surfaces, written now or before, in id order: what an event about the proposal is keyed by. Not on the wire.</param>
public sealed record ProposeResult(MapVersion Draft, int Added, int Existing, int Rejected, [property: JsonIgnore] IReadOnlyList<OverrideId>? Rows = null)
{
    /// <summary>The rows, never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<OverrideId> RowsOrEmpty => Rows ?? [];
}
