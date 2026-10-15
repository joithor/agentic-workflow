using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Reads the public events after the cursor from the ledger (API spec §5.2): the type and item filters run in the ledger scan
/// (S1-D6's engine-key and instance filters here), then <see cref="CallerEventView"/> decides what the caller sees of each row. It reads on,
/// page by page, while scope dropped rows from a full page, until it has <see cref="StreamEventsRequest.Max"/> events or the scan
/// ends, and reports how far it looked, so a filtered stream's cursor moves past the rows it skipped. When a row's scope can't be
/// decided the call keeps what it has and the cursor stays before the row.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="view">What the caller sees of a row.</param>
/// <param name="items">Resolves an item named by id or alias.</param>
/// <param name="callers">The caller, for the item resolution.</param>
internal sealed class StreamEventsService(ILedgerReader ledger, CallerEventView view, IItemResolver items, ICallerContextAccessor callers)
    : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemFilter(request, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var filter = new LedgerFilter(request.Types, item.Value);
        var max = Math.Clamp(request.Max, 1, 500);
        var cursor = Math.Max(request.After, 0);
        var kept = new List<PublicEvent>();
        while (kept.Count < max)
        {
            var step = await Step(new Pass(request, filter, max), cursor, kept, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            cursor = step.Value.Cursor;
            if (!step.Value.More)
            {
                break;
            }
        }

        return new StreamEventsResponse(kept, cursor);
    }

    /// <summary>One scan from <paramref name="cursor"/>: adds what the caller may see to <paramref name="kept"/> and says where the cursor is now and whether another scan could find more.</summary>
    private async ValueTask<Result<Progress>> Step(Pass pass, long cursor, List<PublicEvent> kept, CancellationToken cancellationToken)
    {
        var start = cursor;
        var scan = await ledger.Scan(cursor, pass.Max, pass.Filter, cancellationToken).ConfigureAwait(false);
        if (scan.IsError)
        {
            return scan.Error;
        }

        foreach (var row in scan.Value.Events.Where(e => Keeps(pass.Request, e)))
        {
            var shown = await view.Shown(row, includeNonPublic: false, cancellationToken).ConfigureAwait(false);
            if (shown.IsError)
            {
                // the cursor stays at the last row dealt with, never past a row this caller's scope couldn't be decided for
                return kept.Count > 0 ? new Progress(cursor, More: false) : shown.Error;
            }

            cursor = row.Sequence;
            if (shown.Value is { } e)
            {
                kept.Add(e);
            }

            if (kept.Count == pass.Max)
            {
                return new Progress(cursor, More: false);
            }
        }

        cursor = Math.Max(cursor, scan.Value.ScannedTo);
        return new Progress(cursor, More: scan.Value.Events.Count >= pass.Max && cursor > start);
    }

    private async ValueTask<Result<ItemId?>> ItemFilter(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        if (request.Ref is not { } reference)
        {
            return (ItemId?)null;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.InvalidInput.With("an item filter needs a caller");
        }

        var row = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : row.Value.Id;
    }

    private static bool Keeps(StreamEventsRequest request, LedgerEvent e) =>
        (request.Item is not { } item || string.Equals(e.Item, item.Value, StringComparison.Ordinal))
        && (request.Instance is not { } instance || InstanceEvents.Belongs(e, instance));

    private sealed record Pass(StreamEventsRequest Request, LedgerFilter Filter, int Max);

    private readonly record struct Progress(long Cursor, bool More);
}
