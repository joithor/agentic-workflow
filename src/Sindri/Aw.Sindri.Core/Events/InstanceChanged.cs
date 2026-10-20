using System.Text.Json;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The data of a 'workflow.instance-changed' frame on the event stream (API spec §5): an instance's served view changed, so re-query
/// 'GET /workflow/instances/{address}'. It carries no agent text, only where to look and the state to expect.
/// It is derived from the ledger rows the instance mirrored, never stored: a view rebuild writes none and replays none.
/// </summary>
/// <param name="Instance">The instance address.</param>
/// <param name="State">The state it is in after the change (the state it entered; the final state for an ended instance).</param>
/// <param name="Cursor">The ledger sequence of the change. The view is read live from Temporal and carries no sequence, so it can't be compared with this; <paramref name="State"/> is the authority.</param>
public sealed record InstanceChanged(string Instance, string State, long Cursor)
{
    /// <summary>The frame's event name. It is a stream type only (no ledger row has it), so it is not in <see cref="EventCatalog"/>.</summary>
    public const string Type = "workflow.instance-changed";

    /// <summary>The change a mirrored row records, or none when the row isn't a change of an instance's view.</summary>
    /// <param name="row">A public event as the stream reads it.</param>
    /// <returns>The change, or none for another type, an injection row (it changes nothing a client reads) or a payload that can't be read.</returns>
    public static InstanceChanged? From(PublicEvent row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Changing.Contains(row.Type, StringComparer.Ordinal)
            && row.Payload.ValueKind == JsonValueKind.Object
            && Text(row.Payload, "instance") is { Length: > 0 } instance
            && Text(row.Payload, "to") is { } state
                ? new InstanceChanged(instance, state, row.Sequence)
                : null;
    }

    /// <summary>The mirrored types that change what an instance's view serves (an injection row records a plan, not a change).</summary>
    public static IReadOnlyList<string> Changing { get; } =
    [
        EventTypes.WorkflowStarted, EventTypes.WorkflowTransition, EventTypes.WorkflowRefused,
        EventTypes.WorkflowShifted, EventTypes.WorkflowNotified, EventTypes.WorkflowExpired,
    ];

    private static string? Text(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
