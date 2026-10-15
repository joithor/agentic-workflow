using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds every event about an item into the 'item-timeline' view (API spec §3.1): one clean line per event, keyed '&lt;item&gt;/&lt;sequence:D19&gt;' so a range read gives them in order.</summary>
/// <param name="json">The package's AwJson options (the payloads are read through them).</param>
/// <param name="scrubber">Scrubs secrets out of each line.</param>
internal sealed class ItemTimelineProjection(JsonSerializerOptions json, ITextScrubber scrubber) : IProjection
{
    private const int LineMax = 300;

    public IReadOnlyList<string> Views => [ViewNames.ItemTimeline];

    public bool Handles(string eventType) => true;   // any event type can carry an item (the ledger's Item column decides)

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        if (recorded.Item is not { } item)
        {
            return Unit.Value;
        }

        var envelope = recorded.Envelope;
        var line = SafeText.Clean(scrubber, TimelineLine.For(recorded, json), LineMax);
        var row = new TimelineRow(recorded.Sequence, envelope.OccurredAt, envelope.Type, envelope.Actor, line);
        return await rows.Put(ViewNames.ItemTimeline, ItemTimelineKeys.Of(item, recorded.Sequence), row, cancellationToken).ConfigureAwait(false);
    }
}
