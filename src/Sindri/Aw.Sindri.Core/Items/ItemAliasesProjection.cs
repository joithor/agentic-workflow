using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.linked' (with an alias) into the 'item-aliases' view (API spec §3.1): one row per tracker key, naming its item; a re-link moves the key.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ItemAliasesProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ItemAliases];

    public bool Handles(string eventType) => string.Equals(eventType, EventTypes.ItemLinked, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var linked = events.Payload<ItemLinked>(recorded);
        if (linked.IsError)
        {
            return linked.Error;
        }

        return linked.Value.Alias is { } alias
            ? await rows.Put(ViewNames.ItemAliases, alias.Value, Fold(linked.Value, recorded), cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    /// <summary>The row an 'item.linked' with an alias makes.</summary>
    /// <param name="linked">The payload; its alias must be set.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static AliasRow Fold(ItemLinked linked, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(linked);
        ArgumentNullException.ThrowIfNull(recorded);
        return new AliasRow(linked.Alias ?? throw new ArgumentException("An alias row needs an alias", nameof(linked)), linked.Item, linked.Record, recorded.Sequence);
    }
}
