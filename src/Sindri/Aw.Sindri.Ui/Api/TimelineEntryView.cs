namespace Aw.Sindri.Ui.Api;

/// <summary>One line of an item's timeline (API spec §3.1 GetItemTimeline, P3): shown as served, as plain text.</summary>
/// <param name="Sequence">The ledger sequence.</param>
/// <param name="Time">When it happened.</param>
/// <param name="Type">The event type.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Line">One scrubbed line.</param>
internal sealed record TimelineEntryView(long Sequence, DateTimeOffset Time, string Type, string Actor, string Line);
