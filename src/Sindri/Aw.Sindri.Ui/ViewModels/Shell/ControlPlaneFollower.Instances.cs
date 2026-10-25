using System.Threading.Channels;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The follower's instance stream (<see cref="ControlPlaneFollower"/>, continued): 'workflow.instance-changed' frames, which say where to look.</summary>
internal sealed partial class ControlPlaneFollower
{
    // Follows the instance-change stream (opt-in by type alone, for every instance: the frame and the list both read from it) for as long as
    // the follow lasts, opening it again when it ends. It is a second connection to the same route and never touches the resume cursor: a frame has
    // no id, and a stream of only that type opens at the ledger head, so whatever happened while it was closed is read by the re-read each open
    // makes. The stream is opened first and the views read after, so a change in between is a frame, not a gap.
    private async Task WatchInstances(Uri url, CancellationTokenSource stop)
    {
        var pending = new Dictionary<string, InstanceChange>(StringComparer.Ordinal);
        var signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
#pragma warning disable CA2025 // awaited in the finally below, before the caller disposes stop
        var reader = ReadChanges(url, pending, signal.Reader, stop);
#pragma warning restore CA2025
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await foreach (var item in api.InstanceChanges(url, instance: null, stop.Token).ConfigureAwait(true))
                {
                    if (item.Change is { } change)
                    {
                        lock (pending)
                        {
                            pending[change.Instance] = change;   // the newest of a burst: its state is the one to wait for
                        }

                        _ = signal.Writer.TryWrite(item: true);
                    }
                    else
                    {
                        await health.ReadInstances(url, change: null, superseded: null, stop.Token).ConfigureAwait(true);   // open now: whatever it missed is read
                    }
                }

                await Task.Delay(HealthViewModel.RetryEvery, clock, stop.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(true);   // no more waiting for frames; the reader ends with it
            await reader.ConfigureAwait(true);
        }
    }

    private static bool Waiting(Dictionary<string, InstanceChange> pending, string instance)
    {
        lock (pending)
        {
            return pending.ContainsKey(instance);
        }
    }

    // A frame waits one RefreshEvery for the frames that follow it (one re-read per instance per window), then every instance that changed is read.
    private async Task ReadChanges(Uri url, Dictionary<string, InstanceChange> pending, ChannelReader<bool> signal, CancellationTokenSource stop)
    {
        try
        {
            while (await signal.WaitToReadAsync(stop.Token).ConfigureAwait(true))
            {
                _ = signal.TryRead(item: out _);
                await Task.Delay(HealthViewModel.RefreshEvery, clock, stop.Token).ConfigureAwait(true);
                InstanceChange[] batch;
                lock (pending)
                {
                    batch = [.. pending.Values];
                    pending.Clear();
                }

                foreach (var change in batch)
                {
                    await health.ReadInstances(url, change, c => Waiting(pending, c.Instance), stop.Token).ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }
}
