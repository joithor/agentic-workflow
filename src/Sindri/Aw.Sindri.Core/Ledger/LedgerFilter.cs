using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Core.Ledger;

/// <summary>What a <see cref="ILedgerReader.Scan"/> keeps (API spec §5.2, §2.2 ReadLedger).</summary>
/// <param name="Types">The event types to keep, or null for every type.</param>
/// <param name="Item">The work item to keep, or null for every event.</param>
/// <param name="ScanLimit">The most rows one call looks at past its cursor, so a sparse filter never walks the whole ledger.</param>
/// <param name="Instance">The workflow instance whose mirrored rows to keep (<see cref="InstanceEvents.Belongs(LedgerEvent, InstanceAddress)"/>), or null for any. The reader finds them through the idempotency-key index, so a scan costs the instance's rows and ignores <see cref="ScanLimit"/>.</param>
public sealed record LedgerFilter(TypeFilter? Types, ItemId? Item, int ScanLimit = 5_000, InstanceAddress? Instance = null)
{
    /// <summary>Every event, with the default scan limit.</summary>
    public static LedgerFilter All { get; } = new(Types: null, Item: null);

    /// <summary>The last sequence a scan from <paramref name="cursor"/> looks at: the cursor plus the scan limit (at least one row), without overflowing.</summary>
    /// <param name="cursor">The sequence the scan starts after.</param>
    /// <returns>The highest sequence the scan may look at.</returns>
    public long UpperFrom(long cursor)
    {
        var limit = Math.Max(ScanLimit, 1);
        return cursor > long.MaxValue - limit ? long.MaxValue : cursor + limit;
    }

    /// <summary>True when this filter keeps <paramref name="row"/>.</summary>
    /// <param name="row">The ledger row.</param>
    /// <returns>Whether the row's type and item pass.</returns>
    public bool Keeps(LedgerEvent row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (Types is null || Types.Matches(row.Envelope.Type))
            && (Item is not { } item || string.Equals(row.Item, item.Value, StringComparison.Ordinal));
    }

    /// <summary>
    /// The scan result for <paramref name="kept"/>, the rows this filter kept from the (at most <see cref="ScanLimit"/>) rows after
    /// <paramref name="cursor"/>: when the page filled, it ends at its last row; else the scan looked at every row up to the
    /// scan limit or the head <paramref name="head"/> (read before the rows), and the cursor never moves backward.
    /// </summary>
    /// <param name="cursor">The sequence the scan started after.</param>
    /// <param name="head">The ledger's head, read before the rows.</param>
    /// <param name="max">The page size asked for.</param>
    /// <param name="kept">The rows kept, oldest first.</param>
    /// <returns>The scan.</returns>
    public LedgerScan Scanned(long cursor, long head, int max, IReadOnlyList<LedgerEvent> kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        if (kept.Count >= Math.Max(max, 1))
        {
            return new LedgerScan(kept, kept[^1].Sequence);
        }

        var scanned = Math.Min(head, UpperFrom(cursor));
        return new LedgerScan(kept, Math.Max(Math.Max(scanned, kept.Count > 0 ? kept[^1].Sequence : 0), cursor));
    }
}
