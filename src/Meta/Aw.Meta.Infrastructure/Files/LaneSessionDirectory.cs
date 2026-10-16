using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// The live sessions: Claude Code's own, plus the non-Claude lanes the launcher recorded whose agent is up in its tmux pane. A lane source
/// beside the sessions directory, so the lane map and health see a Codex or Cursor lane as "team-lane". With no non-Claude lane recorded
/// it is exactly the sessions directory (no tmux call, no team-config read).
/// </summary>
/// <param name="claude">Claude Code's sessions directory.</param>
/// <param name="config">The team config, for the team name and the lanes.</param>
/// <param name="files">The lane records.</param>
/// <param name="pane">The lanes' tmux sessions.</param>
internal sealed class LaneSessionDirectory(ISessionDirectory claude, IMetaConfig config, ILaneFiles files, ILanePane pane) : ISessionDirectory
{
    public async ValueTask<Result<IReadOnlyList<LiveSession>>> Live(CancellationToken cancellationToken)
    {
        var sessions = await claude.Live(cancellationToken).ConfigureAwait(false);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (sessions.IsError || team.IsError)
        {
            return sessions;   // no team config: nothing to look up (the Claude sessions stand on their own)
        }

        var live = new List<LiveSession>(sessions.Value);
        foreach (var lane in team.Value.Lanes.Where(l => !string.Equals(l, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase)))
        {
            var found = await Up(team.Value.Team, lane, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                live.Add(found);
            }
        }

        return live;
    }

    // A non-Claude lane is up when it has a reported session id and its pane runs the agent's process.
    private async ValueTask<LiveSession?> Up(string team, string lane, CancellationToken cancellationToken)
    {
        var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
        if (record.IsError || record.Value is not { IsNonClaude: true, Session: { } session, Agent: { } agent })
        {
            return null;
        }

        var state = await pane.Probe(team + "-" + lane, cancellationToken).ConfigureAwait(false);
        return state.IsSuccess && state.Value is { } found && string.Equals(found.Command, agent.ProcessName, StringComparison.Ordinal)
            ? new LiveSession(session, team + "-" + lane, Pid: 0, Status: "idle", Cwd: string.Empty)
            : null;
    }
}
