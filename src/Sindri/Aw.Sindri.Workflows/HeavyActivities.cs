using System.Diagnostics;
using Aw.Sindri.Core.Locks;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'TakeHeavy' and 'ReleaseHeavy' (LN-WF3, line spec §8.4): the engine's allocator for 'waitFor: { resource: heavy }' over the shared
/// heavy-job lock (<see cref="IHeavySlots"/>), holder 'sindri &lt;instance address&gt;'. A take waits in short slices, heartbeating between them, so the
/// workflow can cancel it when the state's timer fires or a command arrives; a busy answer is "still waiting", never a failure. The lease it
/// gets is kept in <see cref="HeavyLeases"/> under the address, and a second take for an address that holds one is answered at once.
/// </summary>
/// <param name="slots">The box-wide lock.</param>
/// <param name="leases">The leases this worker holds.</param>
internal sealed class HeavyActivities(IHeavySlots slots, HeavyLeases leases)
{
    private static readonly TimeSpan _slice = TimeSpan.FromSeconds(5);

    /// <summary>The activity: <see cref="Take"/> under the activity's cancellation, heartbeating between slices.</summary>
    /// <param name="take">Who asks and how long to wait.</param>
    /// <returns>True when a slot is held for the address now.</returns>
    [Activity("TakeHeavy")]
    public Task<bool> TakeAsync(HeavyTake take) => Take(take, static () => ActivityExecutionContext.Current.Heartbeat(), ActivityExecutionContext.Current.CancellationToken);

    /// <summary>The activity: gives back the slot held for the instance, and covers the takes it started.</summary>
    /// <param name="release">The instance and its latest take.</param>
    /// <returns>Done.</returns>
    [Activity("ReleaseHeavy")]
    public async Task ReleaseAsync(HeavyRelease release)
    {
        ArgumentNullException.ThrowIfNull(release);
        await leases.Release(release.Address, release.Attempt).ConfigureAwait(false);
    }

    /// <summary>Takes a slot for the instance, waiting until its bound or a cancel.</summary>
    /// <param name="take">Who asks and how long to wait.</param>
    /// <param name="heartbeat">Told between slices.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>True when a slot is held; false when the bound ran out or the wait was cancelled. The lock directory being unusable throws.</returns>
    public async Task<bool> Take(HeavyTake take, Action heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(take);
        ArgumentNullException.ThrowIfNull(heartbeat);
        if (leases.Holds(take.Address))
        {
            return true;
        }

        var started = Stopwatch.GetTimestamp();
        while (!cancellationToken.IsCancellationRequested)
        {
            var left = take.Bound - Stopwatch.GetElapsedTime(started);
            if (await Slice(take, left < _slice ? left : _slice, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            if (take.Bound - Stopwatch.GetElapsedTime(started) <= TimeSpan.Zero)
            {
                return false;
            }

            heartbeat();
        }

        return false;
    }

    private async Task<bool> Slice(HeavyTake take, TimeSpan wait, CancellationToken cancellationToken)
    {
        try
        {
            var taken = await slots.Take("sindri " + take.Address, wait < TimeSpan.Zero ? TimeSpan.Zero : wait, waiting: null, cancellationToken).ConfigureAwait(false);
            if (taken.IsError)
            {
                throw new InvalidOperationException($"{taken.Error.Code}: {taken.Error.Message}");   // the lock directory can't be used: Temporal retries, then the workflow keeps waiting
            }

            if (taken.Value.Lease is not { } lease)
            {
                return false;
            }

            await leases.Add(take.Address, take.Attempt, lease).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;   // boundary: the workflow cancelled the wait (timer, command or end)
        }
    }
}
