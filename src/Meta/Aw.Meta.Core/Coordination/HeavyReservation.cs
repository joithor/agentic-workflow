using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Sets the heavy-slot reservation from the merge-queue depth (the ready pull requests not yet merged): written before each gate
/// run and after each merge. Only a change is written, and each change is one NOTE line in the running log. A team that sets
/// "heavy": {"reserve": false} never has the file touched. Every gate step carries AW_HEAVY_PRIORITY=meta, so the reservation is
/// safe only while every gate runs through aw-meta gate-run: a gate that runs without the priority waits behind its own reservation.
/// </summary>
/// <param name="reserve">The cap and reservation files.</param>
/// <param name="queue">Reads the merge queue.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the log line.</param>
public sealed class HeavyReservation(IHeavyReserve reserve, MergeQueueReader queue, IRunningLog log, TimeProvider clock)
{
    /// <summary>Sets the reservation for the queue as it stands.</summary>
    /// <param name="team">The team config (its reserve tiers).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success (also when the team turned the reservation off), or the store's or the file's error.</returns>
    public async ValueTask<Result<Unit>> Apply(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var setting = team.Heavy.EffectiveReserve;
        if (!setting.Enabled)
        {
            return Results.Ok();
        }

        var depth = await queue.Read(cancellationToken).ConfigureAwait(false);
        var current = await reserve.Read(cancellationToken).ConfigureAwait(false);
        if (depth.IsError || current.IsError)
        {
            return depth.IsError ? depth.Error : current.Error;
        }

        var wanted = setting.For(depth.Value.Count, current.Value.Cap);
        if (wanted == current.Value.Reserve)
        {
            return Results.Ok();
        }

        var written = await reserve.Write(wanted, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written;
        }

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"heavy reserve {current.Value.Reserve} -> {wanted} of {current.Value.Cap} slots (merge queue depth {depth.Value.Count})");
        return await log.Append(LogLine.Event(LogKind.Note, clock.GetUtcNow(), text), cancellationToken).ConfigureAwait(false);
    }
}
