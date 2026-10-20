namespace Aw.Sindri.Core.Events;

/// <summary>
/// The data of a 'workflow.instance-changed' frame on the event stream (API spec §5): an instance's served view changed, so re-query
/// 'GET /workflow/instances/{address}'. It carries no agent text and no state to apply, only where to look and how fresh the answer must be.
/// It is derived from the ledger rows the instance mirrored, never stored: a view rebuild writes none and replays none.
/// </summary>
/// <param name="Instance">The instance address.</param>
/// <param name="State">The state it is in after the change (the state it entered; the final state for an ended instance).</param>
/// <param name="Cursor">The ledger sequence of the change: a re-read that answers a lower 'asOf' sequence is stale.</param>
public sealed record InstanceChanged(string Instance, string State, long Cursor)
{
    /// <summary>The frame's event name. It is a stream type only (no ledger row has it), so it is not in <see cref="EventCatalog"/>.</summary>
    public const string Type = "workflow.instance-changed";

    /// <summary>The change a mirrored row records, or none when the row isn't a change of an instance's view.</summary>
    /// <param name="row">A public event as the stream reads it.</param>
    /// <returns>The change, or none for another type, an injection row (it changes nothing a client reads) or a payload that can't be read.</returns>
    public static InstanceChanged? From(PublicEvent row) => throw new NotSupportedException();
}
