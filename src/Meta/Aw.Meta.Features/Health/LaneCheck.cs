using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>Finds the configured lanes that have no live session. Read-only: the lane map itself is not changed.</summary>
/// <param name="config">The team config.</param>
/// <param name="sessions">Live Claude sessions.</param>
/// <param name="messages">The recorded lane announcements.</param>
/// <param name="lanes">The current lane map.</param>
/// <param name="standing">How each non-Claude lane stands.</param>
/// <param name="contexts">The lanes' context windows.</param>
internal sealed class LaneCheck(IMetaConfig config, ISessionDirectory sessions, IMessageStore messages, ILaneStore lanes, LaneHealthReader standing, LaneContextReader contexts)
{
    /// <summary>Each lane's context size; null without a team config or when it can't be read.</summary>
    internal async ValueTask<IReadOnlyList<LaneContext>?> Context(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return await contexts.ReadOrNull(team.IsSuccess ? team.Value : null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>How each non-Claude lane stands (taking part, launch-only, hooks not running...); null for a team of Claude lanes or with no team config.</summary>
    internal async ValueTask<Result<IReadOnlyList<LaneHealth>?>> Standing(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? Results.Success<IReadOnlyList<LaneHealth>?>(value: null) : team.Error;
        }

        var rows = await standing.Read(team.Value, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value);
    }

    /// <summary>The lanes with no live session; null when there is no team config to check against.</summary>
    internal async ValueTask<Result<IReadOnlyList<string>?>> Missing(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? Results.Success<IReadOnlyList<string>?>(value: null) : team.Error;
        }

        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var inbox = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var previous = await lanes.Current(cancellationToken).ConfigureAwait(false);
        if (live.IsError || inbox.IsError || previous.IsError)
        {
            return live.IsError ? live.Error : inbox.IsError ? inbox.Error : previous.Error;
        }

        var wanted = team.Value.Lanes.Where(lane => !string.Equals(lane, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase)).ToList();
        var announcements = inbox.Value.Where(m => m.Class.Kind == MessageKind.LaneAnnounce).ToList();
        var mapped = LaneMap.Refresh(team.Value.Team, wanted, live.Value, announcements, previous.Value).Entries.Select(e => e.Lane).ToList();
        return Results.Success<IReadOnlyList<string>?>([.. wanted.Where(lane => !mapped.Contains(lane, StringComparer.OrdinalIgnoreCase))]);
    }
}
