using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Core.Coordination;

/// <summary>What the stop hook does before judging: sync the transcript, refresh the lane map, evaluate what is owed.</summary>
/// <param name="sync">Reads new peer messages.</param>
/// <param name="refresher">Refreshes the lane map.</param>
/// <param name="reader">Evaluates the open obligations.</param>
public sealed class MetaSweep(InboxSync sync, LaneRefresher refresher, ObligationReader reader)
{
    /// <summary>Runs the sweep.</summary>
    /// <param name="transcript">The meta session's transcript.</param>
    /// <param name="config">The team config.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The open obligations.</returns>
    public async ValueTask<Result<IReadOnlyList<Obligation>>> Run(string transcript, TeamConfig config, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        var synced = await sync.Run(transcript, cancellationToken).ConfigureAwait(false);
        if (synced.IsError)
        {
            return synced.Error;
        }

        var refreshed = await refresher.Run(config, now, cancellationToken).ConfigureAwait(false);
        return refreshed.IsError ? refreshed.Error : await reader.Open(config.MetaLane, now, cancellationToken).ConfigureAwait(false);
    }
}
