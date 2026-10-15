using System.Text.Json;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds every event about an item into the 'item-timeline' view (API spec §3.1): one clean line per event, keyed '&lt;item&gt;/&lt;sequence:D19&gt;' so a range read gives them in order.</summary>
/// <param name="json">The package's AwJson options (the payloads are read through them).</param>
/// <param name="scrubber">Scrubs secrets out of each line.</param>
internal sealed class ItemTimelineProjection(JsonSerializerOptions json, ITextScrubber scrubber) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ItemTimeline];

    public bool Handles(string eventType) => throw new NotSupportedException("ItemTimelineProjection.Handles: P3 unit C makes it pass");

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
        => throw new NotSupportedException($"ItemTimelineProjection.Apply: P3 unit C makes it pass ({json is null} {scrubber is null})");

    /// <summary>The timeline key of the event at <paramref name="sequence"/> about <paramref name="item"/>.</summary>
    /// <param name="item">The item id.</param>
    /// <param name="sequence">The ledger sequence.</param>
    /// <returns>'&lt;item&gt;/&lt;sequence:D19&gt;'.</returns>
    public static string Key(string item, long sequence)
        => throw new NotSupportedException("ItemTimelineProjection.Key: P3 unit C makes it pass");
}
