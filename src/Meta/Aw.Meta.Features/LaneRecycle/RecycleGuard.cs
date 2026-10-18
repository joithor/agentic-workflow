using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>The refusals before a recycle: an unknown lane, a lane that isn't Claude Code, a lane with no session, and a digest that is missing, empty or old.</summary>
/// <param name="files">The lane records.</param>
/// <param name="sessions">The session the lane's Claude runs now.</param>
/// <param name="digests">The digest files.</param>
/// <param name="context">Measures the lane before the recycle.</param>
/// <param name="clock">The current time.</param>
internal sealed class RecycleGuard(ILaneFiles files, ILaneSessions sessions, IDigestFiles digests, LaneContextReader context, TimeProvider clock)
{
    internal static readonly TimeSpan DigestMaxAge = TimeSpan.FromHours(2);

    internal async ValueTask<Result<RecyclePlan>> Check(TeamConfig team, LaneRecycleRequest request, CancellationToken cancellationToken)
    {
        var name = team.Lanes.FirstOrDefault(l => string.Equals(l, request.Lane, StringComparison.OrdinalIgnoreCase) && !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return MetaErrors.UnknownLane.With($"'{request.Lane}' is not a lane of team {team.Team}");
        }

        var record = await files.Record(name, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        if (record.Value is { IsNonClaude: true } other)
        {
            return MetaErrors.RecycleRefused.With($"lane {name} runs {other.Agent?.Agent}: only Claude Code lanes are recycled");
        }

        if (await Known(team.Team + "-" + name, record.Value, cancellationToken).ConfigureAwait(false) is not { } session)
        {
            return MetaErrors.LaneNotRecorded.With($"lane {name} has no session: nothing in tmux session {team.Team}-{name} reports one");
        }

        var digest = await CheckDigest(request.Digest, cancellationToken).ConfigureAwait(false);
        if (digest.IsError)
        {
            return digest.Error;
        }

        var before = await context.ReadLane(team.Team, name, team.Context.RecycleAt, cancellationToken).ConfigureAwait(false);
        return new RecyclePlan(team.Team, name, session, before.IsSuccess ? before.Value.Tokens : null, request.Digest);
    }

    // The session id the lane's Claude reports now, else the one lane-sessions holds.
    private async ValueTask<Core.Ids.SessionId?> Known(string tmuxSession, Core.Lanes.LaneRecord? record, CancellationToken cancellationToken)
    {
        var current = await sessions.Current(tmuxSession, cancellationToken).ConfigureAwait(false);
        return (current.IsSuccess ? current.Value : null) ?? record?.Session;
    }

    private async ValueTask<Result<Unit>> CheckDigest(string path, CancellationToken cancellationToken)
    {
        var stat = await digests.Stat(path, cancellationToken).ConfigureAwait(false);
        if (stat.IsError)
        {
            return stat.Error;
        }

        return stat.Value switch
        {
            null => MetaErrors.RecycleRefused.With($"the digest {path} does not exist"),
            { Length: 0 } => MetaErrors.RecycleRefused.With($"the digest {path} is empty"),
            { } info when clock.GetUtcNow() - info.ModifiedAt > DigestMaxAge => MetaErrors.RecycleRefused.With($"the digest {path} is older than 2 hours: ask the lane for a fresh one"),
            _ => Unit.Value,
        };
    }
}
