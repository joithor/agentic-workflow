using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Usage;

/// <summary>Looks at each lane's pane and transcript, and the meta's, for a usage-limit stop.</summary>
/// <param name="files">The lane records.</param>
/// <param name="sessions">The session each Claude lane runs now.</param>
/// <param name="transcripts">Claude Code's transcripts.</param>
/// <param name="pane">The lanes' tmux sessions.</param>
/// <param name="messages">The registered meta session.</param>
/// <param name="clock">The current time and local zone.</param>
public sealed class UsageReader(ILaneFiles files, ILaneSessions sessions, IClaudeTranscripts transcripts, ILanePane pane, IMessageStore messages, TimeProvider clock)
{
    /// <summary>The most of a transcript's end that is read.</summary>
    public const int TailBytes = 256 * 1024;

    private sealed record Look(bool Readable, UsageStop? Stop, string? Why);

    private sealed record PaneLook(bool Readable, PaneUsage? Usage, string? Why);

    /// <summary>Reads every lane but the meta lane, then the meta.</summary>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One reading per subject.</returns>
    public async ValueTask<Result<IReadOnlyList<UsageReading>>> Read(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var rows = new List<UsageReading>();
        foreach (var lane in team.Lanes.Where(l => !string.Equals(l, team.MetaLane, StringComparison.OrdinalIgnoreCase)))
        {
            var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                rows.Add(new UsageReading(lane, UsageState.Unknown, Stop: null, Idle: false, "the lane record can't be read: " + record.Error.Message.ReplaceLineEndings(" ")));
                continue;
            }

            var transcript = record.Value is { IsNonClaude: true } ? new Look(Readable: false, Stop: null, "a non-Claude lane has no Claude transcript") : await LaneTranscript(team.Team + "-" + lane, record.Value?.Session, cancellationToken).ConfigureAwait(false);
            rows.Add(await Combine(lane, team.Team + "-" + lane, transcript, cancellationToken).ConfigureAwait(false));
        }

        var role = await messages.Role(cancellationToken).ConfigureAwait(false);
        var metaSession = team.Team + "-" + team.MetaLane;
        var metaTranscript = role.IsError || role.Value is null
            ? new Look(Readable: false, Stop: null, "no meta session is registered (aw-meta role-set)")
            : await LaneTranscript(metaSession, role.Value.Session, cancellationToken).ConfigureAwait(false);
        rows.Add(await Combine(UsageWatch.Meta, metaSession, metaTranscript, cancellationToken).ConfigureAwait(false));
        return rows;
    }

    /// <summary>Reads, or null when the look failed (the poll goes on without usage).</summary>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The readings or null.</returns>
    public async ValueTask<IReadOnlyList<UsageReading>?> ReadOrNull(TeamConfig? team, CancellationToken cancellationToken)
    {
        if (team is null)
        {
            return null;
        }

        var rows = await Read(team, cancellationToken).ConfigureAwait(false);
        return rows.IsSuccess ? rows.Value : null;
    }

    // The live id first (a /clear changes it), else the recorded one.
    private async ValueTask<Look> LaneTranscript(string tmuxSession, SessionId? recorded, CancellationToken cancellationToken)
    {
        var live = await sessions.Current(tmuxSession, cancellationToken).ConfigureAwait(false);
        var id = (live.IsSuccess ? live.Value : null) ?? recorded;
        if (id is not { } session)
        {
            return new Look(Readable: false, Stop: null, "no session id found");
        }

        var tail = await transcripts.Tail(session, TailBytes, cancellationToken).ConfigureAwait(false);
        if (tail.IsError)
        {
            return new Look(Readable: false, Stop: null, "the transcript can't be read: " + tail.Error.Message.ReplaceLineEndings(" "));
        }

        if (tail.Value is not { } read)
        {
            return new Look(Readable: false, Stop: null, "no transcript found for the session");
        }

        return new Look(Readable: true, UsageTranscript.Read(read.Lines, read.ModifiedAt, clock.LocalTimeZone), Why: null);
    }

    private async ValueTask<PaneLook> Pane(string tmuxSession, DateTimeOffset reference, CancellationToken cancellationToken)
    {
        var probe = await pane.Probe(tmuxSession, cancellationToken).ConfigureAwait(false);
        if (probe.IsError || probe.Value is null)
        {
            return new PaneLook(Readable: false, Usage: null, probe.IsError ? "tmux can't be read: " + probe.Error.Message.ReplaceLineEndings(" ") : $"no tmux session {tmuxSession}");
        }

        var capture = await pane.Capture(tmuxSession, cancellationToken).ConfigureAwait(false);
        return capture.IsError
            ? new PaneLook(Readable: false, Usage: null, "the pane can't be read: " + capture.Error.Message.ReplaceLineEndings(" "))
            : new PaneLook(Readable: true, UsagePane.Read(capture.Value, reference, clock.LocalTimeZone), Why: null);
    }

    private async ValueTask<UsageReading> Combine(string subject, string tmuxSession, Look transcript, CancellationToken cancellationToken)
    {
        var screen = await Pane(tmuxSession, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (!transcript.Readable && !screen.Readable)
        {
            return new UsageReading(subject, UsageState.Unknown, Stop: null, Idle: false, string.Join("; ", new[] { screen.Why, transcript.Why }.Where(static w => w is not null)));
        }

        var fromPane = screen.Usage is { Stop: not null, Current: true } ? screen.Usage : null;
        return Verdict(subject, fromPane, transcript.Stop);
    }

    // Limited when either source shows a stop; the stop with a reset time wins.
    private static UsageReading Verdict(string subject, PaneUsage? fromPane, UsageStop? fromTranscript)
    {
        if (fromPane is null && fromTranscript is null)
        {
            return new UsageReading(subject, UsageState.Ok, Stop: null, Idle: false);
        }

        // The transcript's reset time is worked out from when the message was written, so it is the better one.
        var stops = new[] { fromTranscript, fromPane?.Stop }.OfType<UsageStop>().ToList();
        var stop = stops.Find(static s => s.ResetsAt is not null) ?? stops[0];
        var source = fromPane is not null && fromTranscript is not null ? "pane+transcript" : fromPane is not null ? "pane" : "transcript";
        return new UsageReading(subject, UsageState.Limited, stop, fromPane?.Idle ?? false, Reason: null, source);
    }
}
