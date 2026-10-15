using System.Net.ServerSentEvents;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/events: the ledger as a server-sent event stream (factory addendum §17.21). A client resumes from its
/// own cursor with ?after=N or the Last-Event-ID header; the stream polls the ledger once a second. It ends cleanly when the
/// client goes away or a stop is requested, during a read or a wait, so a stop doesn't wait out Kestrel's graceful-shutdown timeout.
/// ?item=&lt;id&gt; or ?instance=&lt;address&gt; keeps only that item's or that instance's events (engine spec S1-D6); the
/// cursor still moves past the rows the filter skips.
/// </summary>
internal static class EventStream
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static IResult Stream(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service, TimeProvider clock, IShutdown shutdown, long? after)
    {
        var filter = Filter(context.Request.Query["item"], context.Request.Query["instance"]);
        if (filter.IsError)
        {
            return TypedResults.Problem(title: filter.Error.Code, detail: filter.Error.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var cursor = after ?? (long.TryParse(context.Request.Headers["Last-Event-ID"], out var last) ? last : 0);
        return TypedResults.ServerSentEvents(Events(service, clock, filter.Value with { After = cursor }, (context.RequestAborted, shutdown.Stopping)));
    }

    /// <summary>The S1-D6 filter: an item id, an instance address, or neither.</summary>
    internal static Result<StreamEventsRequest> Filter(string? item, string? instance)
    {
        ItemKey? itemKey = null;
        InstanceAddress? address = null;
        if (item is { Length: > 0 })
        {
            if (!ItemKey.TryFrom(item, out var parsed))
            {
                return SindriErrors.InvalidInput.With($"'{item}' isn't an item id (fix: ?item=T-1)");
            }

            itemKey = parsed;
        }

        if (instance is { Length: > 0 })
        {
            if (!InstanceAddress.TryFrom(instance, out var parsed))
            {
                return SindriErrors.InvalidInput.With($"'{instance}' isn't an instance address (fix: ?instance=T-1/step/1)");
            }

            address = parsed;
        }

        return new StreamEventsRequest(0, Item: itemKey, Instance: address);
    }

    internal static async IAsyncEnumerable<SseItem<PublicEvent>> Events(
        IService<StreamEventsRequest, StreamEventsResponse> service, TimeProvider clock, StreamEventsRequest request, (CancellationToken Aborted, CancellationToken Stopping) until)
    {
        using var either = CancellationTokenSource.CreateLinkedTokenSource(until.Aborted, until.Stopping);
        var ct = either.Token;
        var cursor = request.After;
        while (!ct.IsCancellationRequested)
        {
            Result<StreamEventsResponse> page;
            try
            {
                page = await service.Handle(request with { After = cursor }, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                yield break;   // the client left or a stop came while the ledger was read
            }

            foreach (var e in page.IsSuccess ? page.Value.Events : [])
            {
                cursor = e.Sequence;
                yield return new SseItem<PublicEvent>(e, e.Type) { EventId = e.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            }

            cursor = page.IsSuccess ? Math.Max(cursor, page.Value.ScannedTo) : cursor;

            try
            {
                await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }
}
