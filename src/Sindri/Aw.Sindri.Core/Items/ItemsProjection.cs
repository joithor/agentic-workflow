using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.created', 'item.state-changed' and 'item.linked' into the 'items' view (API spec §3.1): one row per item, keyed by its id, its text scrubbed and escaped.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
/// <param name="scrubber">Scrubs secrets out of the text a row stores.</param>
internal sealed class ItemsProjection(EventFactory events, ITextScrubber scrubber) : IProjection
{
    private const int TitleMax = 200;
    private const int BodyMax = 4000;

    public IReadOnlyList<string> Views => [ViewNames.Items];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ItemCreated, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ItemStateChanged, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ItemLinked, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        if (string.Equals(recorded.Envelope.Type, EventTypes.ItemCreated, StringComparison.Ordinal))
        {
            var created = events.Payload<ItemCreated>(recorded);
            return created.IsError
                ? created.Error
                : await rows.Put(ViewNames.Items, created.Value.Item.Value, Fold(created.Value, recorded, scrubber), cancellationToken).ConfigureAwait(false);
        }

        var next = string.Equals(recorded.Envelope.Type, EventTypes.ItemStateChanged, StringComparison.Ordinal)
            ? await Change<ItemStateChanged>(recorded, rows, static changed => changed.Item, Fold, cancellationToken).ConfigureAwait(false)
            : await Change<ItemLinked>(recorded, rows, static linked => linked.Item, Fold, cancellationToken).ConfigureAwait(false);
        return next.IsError ? next.Error : await rows.Put(ViewNames.Items, next.Value.Id.Value, next.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row an 'item.created' makes: state 'new', version 1.</summary>
    /// <param name="created">The payload.</param>
    /// <param name="recorded">The recorded event (its time, actor and sequence).</param>
    /// <param name="scrubber">Scrubs the title and body.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemCreated created, LedgerEvent recorded, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(scrubber);
        var at = recorded.Envelope.OccurredAt;
        return new ItemRow(
            created.Item,
            SafeText.Clean(scrubber, created.Title, TitleMax),
            created.Repo,
            ItemState.New,
            Step: null,
            Version: 1,
            recorded.Envelope.Actor,
            at,
            at,
            at,
            created.Link,
            Alias: null,
            created.Body is null ? null : SafeText.CleanBlock(scrubber, created.Body, BodyMax),
            WaitingOn: null,
            Instance: null,
            created.Owner,
            recorded.Sequence);
    }

    /// <summary>The row after an 'item.state-changed': the new state and version, entered now.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="changed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemRow row, ItemStateChanged changed, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(recorded);
        var at = recorded.Envelope.OccurredAt;
        return row with { State = changed.To, Version = changed.Version, Since = at, UpdatedAt = at, Sequence = recorded.Sequence };
    }

    /// <summary>The row after an 'item.linked': the record and its alias.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="linked">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ItemRow Fold(ItemRow row, ItemLinked linked, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(linked);
        ArgumentNullException.ThrowIfNull(recorded);
        return row with { Link = linked.Record, Alias = linked.Alias, UpdatedAt = recorded.Envelope.OccurredAt, Sequence = recorded.Sequence };
    }

    private async ValueTask<Result<ItemRow>> Change<TPayload>(
        LedgerEvent recorded,
        IViewRows rows,
        Func<TPayload, Ids.ItemId> item,
        Func<ItemRow, TPayload, LedgerEvent, ItemRow> fold,
        CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var id = item(payload.Value);
        var row = await rows.Get<ItemRow>(ViewNames.Items, id.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is null
            ? SindriErrors.ViewsStalled.With($"{recorded.Envelope.Type} for {id.Value}, which has no row")
            : fold(row.Value, payload.Value, recorded);
    }
}
