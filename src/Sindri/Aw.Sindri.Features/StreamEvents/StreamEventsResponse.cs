namespace Aw.Sindri.Features.StreamEvents;

/// <summary>A page of events, oldest first, and the last sequence read (with a filter, rows it skipped still move the cursor).</summary>
/// <param name="Events">The events.</param>
public sealed record StreamEventsResponse(IReadOnlyList<EventView> Events)
{
    /// <summary>The last ledger sequence read for this page, whether or not the filter kept it; null when nothing was read.</summary>
    public long? Scanned { get; init; }
}
