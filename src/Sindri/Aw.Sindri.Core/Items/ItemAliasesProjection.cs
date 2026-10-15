using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.linked' (with an alias) into the 'item-aliases' view (API spec §3.1): one row per tracker key, naming its item; a re-link moves the key.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ItemAliasesProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ItemAliases];

    public bool Handles(string eventType) => throw new NotSupportedException("ItemAliasesProjection.Handles: P3 unit C makes it pass");

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
        => throw new NotSupportedException($"ItemAliasesProjection.Apply: P3 unit C makes it pass ({events is null})");

    /// <summary>The row an 'item.linked' with an alias makes.</summary>
    /// <param name="linked">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static AliasRow Fold(ItemLinked linked, LedgerEvent recorded)
        => throw new NotSupportedException("ItemAliasesProjection.Fold: P3 unit C makes it pass");
}
