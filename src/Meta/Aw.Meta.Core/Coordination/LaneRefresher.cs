using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Maps the configured lanes to the live sessions and saves the map when it changed. Cheap: one directory listing and two small reads.</summary>
/// <param name="sessions">The live sessions.</param>
/// <param name="messages">The lane announcements.</param>
/// <param name="lanes">Stores the map.</param>
public sealed class LaneRefresher(ISessionDirectory sessions, IMessageStore messages, ILaneStore lanes)
{
    /// <summary>Refreshes the map.</summary>
    /// <param name="config">The team config.</param>
    /// <param name="now">When the refresh happens, for a changed map's history.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The map and whether it changed.</returns>
    public async ValueTask<Result<LaneRefresh>> Run(TeamConfig config, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = await lanes.Current(cancellationToken).ConfigureAwait(false);
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var inbox = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (previous.IsError || live.IsError || inbox.IsError)
        {
            return previous.IsError ? previous.Error : live.IsError ? live.Error : inbox.Error;
        }

        var announcements = inbox.Value.Where(m => m.Class.Kind == MessageKind.LaneAnnounce).ToList();
        var map = LaneMap.Refresh(config.Team, config.Lanes, live.Value, announcements, previous.Value);
        if (map.Version == previous.Value.Version)
        {
            return new LaneRefresh(map, Changed: false);
        }

        var saved = await lanes.Save(map, now, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new LaneRefresh(map, Changed: true);
    }
}
