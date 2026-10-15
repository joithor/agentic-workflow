using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The event stream's loop (API spec §5.2, §5.4, §5.5): read a page after the cursor, write each event as a frame, and when caught
/// up wait on the ledger signal (a committed append wakes it at once; the one-second poll is the fallback), writing a ': ping'
/// comment after 15 seconds without a write. A full page, or a cursor still behind the head, reads again at once. A read error is
/// sent as an 'event: error' frame and ends the stream; so does the client leaving or a stop, without a frame.
/// </summary>
/// <param name="ledger">The ledger, for its head.</param>
/// <param name="signal">Wakes the wait when an append commits.</param>
/// <param name="clock">Times the idle ping and the wait.</param>
/// <param name="json">The package's AwJson options, for the frames' data.</param>
internal sealed class EventPump(ILedgerReader ledger, ILedgerSignal signal, TimeProvider clock, JsonSerializerOptions json)
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(15);

    /// <summary>Streams from <paramref name="request"/>'s cursor to <paramref name="writer"/> until <paramref name="cancellationToken"/> is cancelled, the client is gone or a read fails.</summary>
    /// <param name="writer">Where the frames go.</param>
    /// <param name="service">Reads each page.</param>
    /// <param name="request">The filters and the first cursor (its After).</param>
    /// <param name="cancellationToken">Ends the stream: the client left or a stop was requested.</param>
    /// <returns>When the stream has ended.</returns>
    public async Task Run(SseWriter writer, IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var cursor = request.After;
        var lastWrite = clock.GetTimestamp();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var next = await NextPage(service, request with { After = cursor }, cancellationToken).ConfigureAwait(false);
                if (next.IsError)
                {
                    await writer.Event(id: null, "error", Text(new StreamErrorFrame(next.Error.Code, next.Error.Message)), cancellationToken).ConfigureAwait(false);
                    return;
                }

                var (page, head) = next.Value;
                foreach (var e in page.Events)
                {
                    await writer.Event(e.Sequence, e.Type, Text(e), cancellationToken).ConfigureAwait(false);
                    lastWrite = clock.GetTimestamp();
                }

                cursor = Math.Max(cursor, page.ScannedTo);
                if (page.Events.Count < request.Max && cursor >= head)
                {
                    lastWrite = await WaitOrPing(writer, cursor, lastWrite, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the client left or a stop came, during a read, a wait or a write: the stream ends cleanly
        }
        catch (IOException)
        {
            // the client's connection broke mid-write
        }
    }

    private async ValueTask<Result<(StreamEventsResponse Page, long Head)>> NextPage(IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var page = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);   // after the page: an append in between only means another read at once
        return head.IsError ? head.Error : (page.Value, head.Value);
    }

    /// <summary>Waits for a pulse past <paramref name="cursor"/> for at most a second, or writes the ping when the stream has been idle that long; returns the time of the last write.</summary>
    private async ValueTask<long> WaitOrPing(SseWriter writer, long cursor, long lastWrite, CancellationToken cancellationToken)
    {
        var untilPing = PingEvery - clock.GetElapsedTime(lastWrite);
        if (untilPing <= TimeSpan.Zero)
        {
            await writer.Comment("ping", cancellationToken).ConfigureAwait(false);
            return clock.GetTimestamp();
        }

        _ = await signal.WaitAfter(cursor, untilPing < Poll ? untilPing : Poll, cancellationToken).ConfigureAwait(false);
        return lastWrite;
    }

    private string Text<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, json));
}
