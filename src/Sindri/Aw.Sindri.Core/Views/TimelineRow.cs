namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'item-timeline' view, keyed '&lt;itemId&gt;/&lt;sequence:D19&gt;': one scrubbed line per event about the item.</summary>
/// <param name="Sequence">The event's ledger sequence.</param>
/// <param name="Time">When it happened.</param>
/// <param name="Type">Its event type.</param>
/// <param name="Actor">Who did it, verbatim from the envelope.</param>
/// <param name="Line">The one line the timeline shows.</param>
public sealed record TimelineRow(long Sequence, DateTimeOffset Time, string Type, string Actor, string Line);
