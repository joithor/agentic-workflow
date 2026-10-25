using System.Text.Json.Serialization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>What proposing wrote; 'current' is never touched.</summary>
/// <param name="Draft">The draft the rows went to (opened as a copy of current when there was none); null when a withdrawal found no draft to touch.</param>
/// <param name="Added">New 'propose' rows.</param>
/// <param name="Existing">Surfaces already in the map or already proposed (idempotent: no row).</param>
/// <param name="Rejected">Surfaces a person rejected earlier (never re-proposed: no row).</param>
/// <param name="Rows">The ids of the 'propose' rows this run has in the draft for the call's surfaces, written now or before, in id order: what an event about the proposal is keyed by. Not on the wire.</param>
public sealed record ProposeResult(MapVersion? Draft, int Added, int Existing, int Rejected, [property: JsonIgnore] IReadOnlyList<OverrideId>? Rows = null)
{
    /// <summary>Undecided proposals of the run that this call withdrew (an empty surface list). Zero otherwise.</summary>
    public int Withdrawn { get; init; }

    /// <summary>The run's proposals a person accepted, merged, renamed or moved, which a withdrawal leaves in place. Zero otherwise.</summary>
    public int Kept { get; init; }

    /// <summary>The ids of the 'undo' rows of the run's latest withdrawal, in id order: the rows this call wrote, or (when it wrote none, a repeat or a retry) the ones the latest withdrawal wrote. What the event about that withdrawal is keyed by, so it is written once. Not on the wire.</summary>
    [JsonIgnore]
    public IReadOnlyList<OverrideId>? WithdrawnRows { get; init; }

    /// <summary>The rows, never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<OverrideId> RowsOrEmpty => Rows ?? [];
}
