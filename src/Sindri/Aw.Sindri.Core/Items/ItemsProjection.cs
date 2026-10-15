using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.created', 'item.state-changed' and 'item.linked' into the 'items' view (API spec §3.1): one row per item, keyed by its id, its text scrubbed and escaped.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
/// <param name="scrubber">Scrubs secrets out of the text a row stores.</param>
internal sealed class ItemsProjection(EventFactory events, ITextScrubber scrubber) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Items];

    public bool Handles(string eventType) => throw new NotSupportedException("ItemsProjection.Handles: P3 unit C makes it pass");

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
        => throw new NotSupportedException($"ItemsProjection.Apply: P3 unit C makes it pass ({events is null} {scrubber is null})");

    /// <summary>The row an 'item.created' makes: state 'new', version 1.</summary>
    /// <param name="created">The payload.</param>
    /// <param name="recorded">The recorded event (its time, actor and sequence).</param>
    /// <param name="scrubber">Scrubs the title and body.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemCreated created, LedgerEvent recorded, ITextScrubber scrubber)
        => throw new NotSupportedException("ItemsProjection.Fold: P3 unit C makes it pass");

    /// <summary>The row after an 'item.state-changed': the new state and version, entered now.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="changed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemRow row, ItemStateChanged changed, LedgerEvent recorded)
        => throw new NotSupportedException("ItemsProjection.Fold: P3 unit C makes it pass");

    /// <summary>The row after an 'item.linked': the record and its alias.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="linked">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemRow row, ItemLinked linked, LedgerEvent recorded)
        => throw new NotSupportedException("ItemsProjection.Fold: P3 unit C makes it pass");
}
