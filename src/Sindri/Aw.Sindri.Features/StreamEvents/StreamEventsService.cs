using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Reads events after the cursor from the ledger. With a filter (S1-D6) it reads on, page by page, until it has
/// <see cref="StreamEventsRequest.Max"/> matching events or reaches the head, and reports how far it read.
/// The item events are the first with free human text, so the stream shows 'item.created' in its public form (no body, a scrubbed and escaped title) and
/// hides a session's view of another repo's item rows (API spec §3.1; P5 owns the full public-type filter, 'command.handled' still passes).
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Reads the item payloads.</param>
/// <param name="scrubber">Scrubs the title of an 'item.created' row.</param>
/// <param name="views">The 'items' view: the repo of an item a state change or link row is about.</param>
/// <param name="callers">The caller, whose repo scope applies when it is a session.</param>
/// <param name="json">The package's AwJson options, for the public payload.</param>
internal sealed class StreamEventsService(ILedgerReader ledger, EventFactory events, ITextScrubber scrubber, IViewReader views, ICallerContextAccessor callers, JsonSerializerOptions json)
    : IService<StreamEventsRequest, StreamEventsResponse>
{
    private const int TitleMax = 200;

    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = Math.Clamp(request.Max, 1, 500);
        var cursor = Math.Max(request.After, 0);
        var kept = new List<EventView>();
        long? scanned = null;
        while (kept.Count < max)
        {
            var page = await ledger.ReadAfter(cursor, max, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var e in page.Value)
            {
                scanned = cursor = e.Sequence;
                if (Keeps(request, e) && await Shown(e, cancellationToken).ConfigureAwait(false) is { } view)
                {
                    kept.Add(view);
                    if (kept.Count == max)
                    {
                        break;
                    }
                }
            }

            if (page.Value.Count < max)
            {
                break;
            }
        }

        return new StreamEventsResponse(kept) { Scanned = scanned };
    }

    private static bool Keeps(StreamEventsRequest request, LedgerEvent e) =>
        (request.Item is not { } item || string.Equals(e.Item, item.Value, StringComparison.Ordinal))
        && (request.Instance is not { } instance || InstanceEvents.Belongs(e, instance));

    /// <summary>The row as this caller may see it, or null when it may not.</summary>
    private async ValueTask<EventView?> Shown(LedgerEvent e, CancellationToken cancellationToken)
    {
        var type = e.Envelope.Type;
        if (string.Equals(type, EventTypes.ItemCreated, StringComparison.Ordinal))
        {
            return Created(e);
        }

        var itemRow = string.Equals(type, EventTypes.ItemStateChanged, StringComparison.Ordinal) || string.Equals(type, EventTypes.ItemLinked, StringComparison.Ordinal);
        return itemRow && callers.Current is { Class: CallerClass.Session } session && !await InScope(e, session, cancellationToken).ConfigureAwait(false) ? null : View(e, e.Envelope.Payload);
    }

    /// <summary>'item.created' in its public form: no body, a scrubbed and escaped title; null for a session outside the item's repo.</summary>
    private EventView? Created(LedgerEvent e)
    {
        var created = events.Payload<ItemCreated>(e);
        if (created.IsError)
        {
            return View(e, "{}");   // an unreadable row has nothing safe to show
        }

        var c = created.Value;
        if (callers.Current is { } caller && !ItemScope.Visible(caller, c.Repo))
        {
            return null;
        }

        var publicForm = new ItemCreatedPublic(c.CommandId, c.Item, SafeText.Clean(scrubber, c.Title, TitleMax), c.Repo, c.Link, c.Owner);
        return View(e, Encoding.UTF8.GetString(AwJson.Serialize(publicForm, json)));
    }

    /// <summary>Whether the item a state change or link row is about is in the session's repo (an item the views don't hold yet isn't).</summary>
    private async ValueTask<bool> InScope(LedgerEvent e, CallerContext session, CancellationToken cancellationToken)
    {
        if (e.Item is null)
        {
            return false;
        }

        var row = await views.Get<ItemRow>(ViewNames.Items, e.Item, cancellationToken).ConfigureAwait(false);
        return row.IsSuccess && row.Value is { } item && ItemScope.Visible(session, item.Repo);
    }

    private static EventView View(LedgerEvent e, string payload) =>
        new(e.Sequence, e.Envelope.Type, e.Envelope.OccurredAt, e.Envelope.Actor, e.Envelope.Tenant, e.Envelope.Ring, e.Epoch, payload);
}
