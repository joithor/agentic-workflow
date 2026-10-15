using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/events: the ledger's public events as a server-sent event stream (API spec §5; factory addendum §17.21). Everything
/// that can refuse the request is decided before the first frame, as a ProblemDetails answer: a bad filter (400), a cursor past the
/// ledger's head (400) and no free stream slot (429, SND-API-012). The cursor is the Last-Event-ID header when it holds a sequence,
/// else ?after=, else 0 (C4). ?item= (an id or tracker alias), ?types= (globs) and ?instance= (engine spec S1-D6) filter the stream.
/// </summary>
/// <param name="ledger">The ledger, for the head.</param>
/// <param name="slots">The open streams.</param>
/// <param name="callers">Who opens the stream.</param>
/// <param name="pump">The loop the answer runs.</param>
/// <param name="shutdown">Ends open streams on stop.</param>
internal sealed class EventStreamOpener(ILedgerReader ledger, IStreamSlots slots, ICallerContextAccessor callers, EventPump pump, IShutdown shutdown)
{
    /// <summary>The answer for <paramref name="context"/>'s request: an open stream or the refusal.</summary>
    /// <param name="context">The request.</param>
    /// <param name="service">Reads each page.</param>
    /// <returns>The stream, or a ProblemDetails answer.</returns>
    public async Task<IResult> Open(HttpContext context, IService<StreamEventsRequest, StreamEventsResponse> service)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = Bind(context.Request);
        if (request.IsError)
        {
            return HttpResults.Problem(request.Error);
        }

        var head = await ledger.Head(context.RequestAborted).ConfigureAwait(false);
        if (head.IsError)
        {
            return HttpResults.Problem(head.Error);
        }

        if (request.Value.After > head.Value)
        {
            return HttpResults.Problem(SindriErrors.InvalidInput.With(
                $"the cursor {request.Value.After.ToString(CultureInfo.InvariantCulture)} is past the ledger's head {head.Value.ToString(CultureInfo.InvariantCulture)} (fix: after={head.Value.ToString(CultureInfo.InvariantCulture)}, or leave it out)"));
        }

        var caller = callers.Current?.Class ?? CallerClass.Human;
        if (!slots.TryEnter(caller, out var slot))
        {
            return HttpResults.Problem(SindriErrors.StreamsFull.With(SindriErrors.StreamsFull.Title));
        }

        StreamTelemetry.Opened(caller, slots.Open(caller));
        return new SseResult(pump, service, request.Value, new Counted(slot, slots, caller), shutdown.Stopping);
    }

    /// <summary>The filters and first cursor a request names, or the 400 for a malformed one.</summary>
    internal static Result<StreamEventsRequest> Bind(HttpRequest http)
    {
        var item = ItemRoute.ItemFilter(http.Query["item"]);
        var types = TypeFilterOf(http.Query["types"]);
        var instance = InstanceOf(http.Query["instance"]);
        var after = Cursor(http.Headers["Last-Event-ID"], http.Query["after"]);
        if (item.IsError || types.IsError || instance.IsError || after.IsError)
        {
            return item.IsError ? item.Error : types.IsError ? types.Error : instance.IsError ? instance.Error : after.Error;
        }

        return new StreamEventsRequest(after.Value, Item: null, Instance: instance.Value, Types: types.Value, Ref: item.Value);
    }

    /// <summary>The first cursor (C4): Last-Event-ID when it holds a sequence, else ?after=, else 0.</summary>
    private static Result<long> Cursor(string? lastEventId, string? after)
    {
        if (long.TryParse(lastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var last))
        {
            return last;
        }

        return after is null || after.Length == 0
            ? 0L
            : long.TryParse(after, NumberStyles.None, CultureInfo.InvariantCulture, out var cursor)
                ? cursor
                : SindriErrors.InvalidInput.With("after must be a ledger sequence, 0 or more (fix: ?after=0)");
    }

    private static Result<TypeFilter?> TypeFilterOf(string? types)
    {
        if (string.IsNullOrEmpty(types))
        {
            return (TypeFilter?)null;
        }

        var parsed = TypeFilter.Parse(types);
        return parsed.IsError ? parsed.Error : parsed.Value;
    }

    private static Result<InstanceAddress?> InstanceOf(string? instance)
    {
        if (string.IsNullOrEmpty(instance))
        {
            return (InstanceAddress?)null;
        }

        return InstanceAddress.TryFrom(instance, out var address)
            ? address
            : SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(instance), 80)}' isn't an instance address (fix: ?instance=T-1/step/1)");
    }

    /// <summary>The slot, with the open-streams gauge updated when it is freed.</summary>
    private sealed class Counted(IDisposable inner, IStreamSlots slots, CallerClass caller) : IDisposable
    {
        public void Dispose()
        {
            inner.Dispose();
            StreamTelemetry.Opened(caller, slots.Open(caller));
        }
    }
}
