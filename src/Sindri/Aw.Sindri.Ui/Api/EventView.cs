namespace Aw.Sindri.Ui.Api;

/// <summary>One ledger event from GET /sindri/events (the fields the app shows; the rest of the record is ignored).</summary>
/// <param name="Sequence">Its ledger position, the stream cursor.</param>
/// <param name="Type">The event type.</param>
/// <param name="OccurredAt">When it happened.</param>
internal sealed record EventView(long Sequence, string Type, DateTimeOffset OccurredAt);
