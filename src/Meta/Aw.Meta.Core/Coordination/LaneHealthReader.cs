using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Says how each non-Claude lane stands: taking part, launch-only, hooks not running, an obligation it can't be shown, or an ambiguous session id. Claude lanes have no row.</summary>
/// <param name="files">The lane records.</param>
/// <param name="beats">The hook heartbeats.</param>
/// <param name="mail">The messages owed.</param>
/// <param name="guard">The stop gate's counts.</param>
public sealed class LaneHealthReader(ILaneFiles files, LaneBeats beats, ILaneMail mail, LaneStopGuard guard)
{
    /// <summary>Cursor ends a stop follow-up chain after this many (its default loop_limit); the gate can't be shown past it.</summary>
    public const int CursorLoopLimit = 5;

    /// <summary>Reads every non-Claude lane's standing.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per non-Claude lane; empty for a team of Claude lanes.</returns>
    public async ValueTask<Result<IReadOnlyList<LaneHealth>>> Read(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var rows = new List<LaneHealth>();
        foreach (var lane in team.Lanes.Where(l => !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase)))
        {
            var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            if (record.Value is not { IsNonClaude: true, Agent: { } agent } found)
            {
                continue;
            }

            var state = await Standing(lane, found, agent, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                return state.Error;
            }

            rows.Add(new LaneHealth(lane, agent.Agent, state.Value.State, state.Value.Attention));
        }

        return rows;
    }

    private async ValueTask<Result<(string State, bool Attention)>> Standing(string lane, LaneRecord record, LaneAgent agent, CancellationToken cancellationToken)
    {
        if (!agent.Hooks)
        {
            return ("launch-only: no obligations", false);
        }

        if (record.Ambiguous || record.Session is null)
        {
            return record.Ambiguous ? ("ambiguous session id", true) : ("waiting for its session id", false);
        }

        var beat = await beats.Last(lane, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        return beat.Value is not { } last || (record.RecordedAt is { } recorded && last.At < recorded)
            ? (HooksNotRunning(agent), true)
            : await Delivery(lane, agent, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<(string State, bool Attention)>> Delivery(string lane, LaneAgent agent, CancellationToken cancellationToken)
    {
        var owed = await mail.Owed(lane, cancellationToken).ConfigureAwait(false);
        if (owed.IsError)
        {
            return owed.Error;
        }

        var state = await guard.State(lane, owed.Value, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        return state.Value switch
        {
            { Capped: true } => ("obligation not delivered (stop cap)", true),
            { Blocks: >= CursorLoopLimit } when string.Equals(agent.Agent, "cursor", StringComparison.Ordinal) => ("obligation not delivered (Cursor loop_limit)", true),
            _ => ("taking part", false),
        };
    }

    /// <summary>The line for a lane whose hooks never reported.</summary>
    /// <param name="agent">The lane's agent.</param>
    /// <returns>The line, naming the fix.</returns>
    public static string HooksNotRunning(LaneAgent agent) =>
        string.Equals(agent.Agent, "codex", StringComparison.Ordinal)
            ? "hooks not running: trust them with /hooks in codex"
            : "hooks not running: check the " + agent.Agent + " hooks are installed";
}
