using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri status --follow [--json]' (API spec §5.1, P5 unit H): prints the status summary, then again each time it changes. It opens
/// the event stream after the ledger head the first status held, re-reads the status on each event (events in the same 250 ms count once,
/// and the read asks to reflect the last one's sequence), and prints only when the summary differs, so a ': ping' or an event that changes
/// nothing prints nothing. With --json it prints one status body per line. A full stream list (SND-API-012) or an error frame falls back to re-reading
/// every 5 s, also when an error frame arrives within a debounce window. Exit 0 on Ctrl-C or SIGTERM; 1 when Sindri isn't running or stops while
/// followed; 2 when the first read fails for another reason. When the views can't catch up to an event (SND-API-011: stalled or rebuilding), the status is
/// read again plain, so the summary still prints. A read verb: it never starts the process.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="probe">Finds the running control plane.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="clock">Times the debounce and the polling fallback.</param>
internal sealed class FollowVerbs(ControlPlaneClient client, Core.Control.IControlPlaneProbe probe, Output output, TimeProvider clock)
{
    internal static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(5);

    internal async Task<int> Run(bool json, CancellationToken ct)
    {
        var shown = new Shown(json);
        try
        {
            var first = await Read(afterSequence: null, shown, ct).ConfigureAwait(false);
            if (first.IsError)
            {
                return await Failed(first.Error).ConfigureAwait(false);
            }

            var head = first.Value.Views?.LedgerHead ?? 0;
            var ended = await FollowStream(head, shown, ct).ConfigureAwait(false);
            return await Failed(ended.IsError ? ended.Error : SindriErrors.NotRunning.With("the control plane stopped")).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return 0;
        }
    }

    /// <summary>Follows the stream until it ends (false: the server stopped it) or falls back to polling, which ends only with an error.</summary>
    private async Task<Result<bool>> FollowStream(long head, Shown shown, CancellationToken ct)
    {
        using var end = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var frames = client.Stream("/sindri/events?after=" + head.ToString(CultureInfo.InvariantCulture), end.Token).GetAsyncEnumerator(end.Token);
        Task<bool>? next = null;
        try
        {
            return await Loop(frames, shown, pending => next = pending, ct).ConfigureAwait(false);
        }
        finally
        {
            // an iterator can't be disposed while a read is outstanding: end the stream, then let the read finish
            await end.CancelAsync().ConfigureAwait(false);
            if (next is not null)
            {
                try
                {
                    _ = await next.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // the read ended with the stream
                }
            }
        }
    }

    private async Task<Result<bool>> Loop(IAsyncEnumerator<Result<StreamFrame>> frames, Shown shown, Action<Task<bool>?> hold, CancellationToken ct)
    {
        Task<bool>? next = null;
        while (true)
        {
            next ??= frames.MoveNextAsync().AsTask();
            hold(next);
            if (!await next.ConfigureAwait(false))
            {
                return false;
            }

            next = null;
            hold(null);
            if (frames.Current.IsError || frames.Current.Value.Event == "error")
            {
                return await PollInstead(shown, ct).ConfigureAwait(false);
            }

            var settled = await Settle(frames, frames.Current.Value.Id, hold, ct).ConfigureAwait(false);
            if (settled.Errored)
            {
                return await PollInstead(shown, ct).ConfigureAwait(false);
            }

            var (last, more, pending) = (settled.Last, settled.More, settled.Pending);
            next = pending;
            var read = await Read(last, shown, ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (!more)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Takes the frames that arrive within the debounce window after one; returns the last sequence, whether the stream is still open, the read left
    /// pending and whether an error frame arrived (the stream is over then: the caller polls).
    /// </summary>
    private async Task<Settled> Settle(IAsyncEnumerator<Result<StreamFrame>> frames, long first, Action<Task<bool>?> hold, CancellationToken ct)
    {
        using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var quiet = Task.Delay(Debounce, clock, window.Token);
        var last = first;
        while (true)
        {
            var pending = frames.MoveNextAsync().AsTask();
            hold(pending);
            if (await Task.WhenAny(pending, quiet).ConfigureAwait(false) == quiet)
            {
                ct.ThrowIfCancellationRequested();
                return new Settled(last, More: true, pending, Errored: false);
            }

            if (!await pending.ConfigureAwait(false))
            {
                await window.CancelAsync().ConfigureAwait(false);
                hold(null);
                return new Settled(last, More: false, Pending: null, Errored: false);
            }

            if (frames.Current.IsError || frames.Current.Value.Event == "error")
            {
                await window.CancelAsync().ConfigureAwait(false);
                hold(null);
                return new Settled(last, More: false, Pending: null, Errored: true);
            }

            last = frames.Current.IsSuccess ? Math.Max(last, frames.Current.Value.Id) : last;
        }
    }

    /// <summary>No stream to follow (too many are open, or it reported an error): re-read every 5 s until the server goes away.</summary>
    private async Task<Result<bool>> PollInstead(Shown shown, CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(Poll, clock, ct).ConfigureAwait(false);
            var read = await Read(afterSequence: null, shown, ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }
        }
    }

    /// <summary>Reads the status and prints it when it differs from the last one printed; an error that isn't "not running" is told on stderr and the follow goes on.</summary>
    private async Task<Result<GetStatusResponse>> Read(long? afterSequence, Shown shown, CancellationToken ct)
    {
        var found = await probe.Check(ct).ConfigureAwait(false);
        var status = found.IsError ? found.Error : await Fetch(afterSequence, ct).ConfigureAwait(false);
        if (status.IsError)
        {
            if (status.Error.Code == SindriErrors.NotRunning.Code || shown.Count == 0)
            {
                return status.Error;
            }

            _ = await output.Error(status.Error).ConfigureAwait(false);
            return shown.Last!;   // why: Count > 0 here, so a status was recorded
        }

        var text = shown.Json ? System.Text.Encoding.UTF8.GetString(status.Value.Body.Span) : SummaryText.Render(status.Value.Value, found.Value.Endpoint, ui: null);
        if (!string.Equals(text, shown.LastText, StringComparison.Ordinal))
        {
            _ = shown.Json
                ? await output.Raw(status.Value.Body, 0).ConfigureAwait(false)
                : await output.Text((shown.Count == 0 ? string.Empty : "\n") + text, 0).ConfigureAwait(false);
            shown.Record(text, status.Value.Value);
        }

        return status.Value.Value;
    }

    /// <summary>The status, reflecting <paramref name="afterSequence"/> when one is given; views that stall or rebuild never reach it (SND-API-011), so the read is repeated plain and the summary says so.</summary>
    private async Task<Result<ApiAnswer<GetStatusResponse>>> Fetch(long? afterSequence, CancellationToken ct)
    {
        if (afterSequence is not { } sequence)
        {
            return await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        }

        var reflected = await client.GetAfter<GetStatusResponse>("/sindri/status", sequence, ct).ConfigureAwait(false);
        return reflected.IsError && reflected.Error.Code == SindriErrors.ViewsBehind.Code
            ? await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false)
            : reflected;
    }

    /// <summary>Tells why the follow ended: exit 1 for 'not running' (the output's attention code), 2 for anything else.</summary>
    private async Task<int> Failed(Error error) =>
        await output.Error(error.Code == SindriErrors.NotRunning.Code ? error with { Message = "not running: " + error.Message } : error).ConfigureAwait(false);

    /// <summary>What a debounce window took: the last sequence, whether the stream is open, the read left pending, and whether an error frame ended it.</summary>
    private readonly record struct Settled(long Last, bool More, Task<bool>? Pending, bool Errored);

    /// <summary>What the follow has printed so far.</summary>
    private sealed class Shown(bool json)
    {
        public bool Json { get; } = json;

        public int Count { get; private set; }

        public string? LastText { get; private set; }

        public GetStatusResponse? Last { get; private set; }

        public void Record(string text, GetStatusResponse status) => (LastText, Last, Count) = (text, status, Count + 1);
    }
}
