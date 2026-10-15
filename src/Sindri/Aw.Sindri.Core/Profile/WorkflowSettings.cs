using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow:' block (engine spec §7.2 S1-D11; engine P4 adds these two keys, P6 and P7 add theirs). Each
/// value is a duration ('1m', '4h', '2d'); a key left out takes its default (<see cref="WorkflowTimes.Defaults"/>).
/// </summary>
/// <param name="DeskTimeout">The floor of every desk Step's timeout (W20; default 2d). The dev profile sets 1m so a desk wait parks fast.</param>
/// <param name="EventHold">How long a buffered event signal waits for its wait before it is dropped with 'workflow.expired' (§5.4; default 24h).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkflowSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeskTimeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EventHold = null);
