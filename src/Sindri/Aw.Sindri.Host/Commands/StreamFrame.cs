namespace Aw.Sindri.Host.Commands;

/// <summary>One frame of an event stream (API spec §5.3): the ledger sequence in 'id', the event type, and the data line (a public event's JSON, or an error frame's).</summary>
/// <param name="Id">The frame's id: the event's ledger sequence (0 for an error frame).</param>
/// <param name="Event">The event type, or 'error'.</param>
/// <param name="Data">The data line.</param>
internal sealed record StreamFrame(long Id, string Event, string Data);
