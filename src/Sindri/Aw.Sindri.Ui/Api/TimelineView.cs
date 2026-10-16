namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/items/{itemId}/timeline?after= (API spec §3.1, P3): a page of entries and the cursor of the next page.</summary>
/// <param name="Entries">The entries, in the API's order.</param>
/// <param name="Next">The 'after' of the next page, or null when this is the last.</param>
/// <param name="AsOf">How fresh the view is.</param>
internal sealed record TimelineView(IReadOnlyList<TimelineEntryView> Entries, long? Next, AsOfView AsOf);
