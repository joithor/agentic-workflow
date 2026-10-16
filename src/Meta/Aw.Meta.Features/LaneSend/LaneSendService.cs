using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneSend;

/// <summary>
/// The meta's send to a non-Claude lane, which has no SendMessage: the message goes into the lane's inbox (its hooks deliver it at
/// the next prompt or stop) and is recorded as a send to "team-lane", which closes the meta's reply obligation the way a
/// SendMessage does. An idle lane whose agent is up and with nobody attached is woken with one typed line.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="files">The lane records.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="messages">Records the send.</param>
/// <param name="beats">The lane hooks' heartbeats.</param>
/// <param name="pane">The lane's tmux session.</param>
/// <param name="clock">Dates the send.</param>
internal sealed class LaneSendService(IMetaConfig config, ILaneFiles files, ILaneMail mail, IMessageStore messages, LaneBeats beats, ILanePane pane, TimeProvider clock)
    : IService<LaneSendRequest, LaneSendResponse>
{
    internal const string WakeLine = "A message from the meta is waiting (aw-meta lane-inbox). Answer it, ending your turn with a line starting REPLY TO META:";

    public async ValueTask<Result<LaneSendResponse>> Handle(LaneSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var name = team.Value.Lanes.FirstOrDefault(l => string.Equals(l, request.Lane, StringComparison.OrdinalIgnoreCase) && !string.Equals(l, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return MetaErrors.UnknownLane.With($"'{request.Lane}' is not a lane of team {team.Value.Team} (lanes: {string.Join(", ", team.Value.Lanes.Where(l => !string.Equals(l, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase)))})");
        }

        var record = await files.Record(name, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        return record.Value is { IsNonClaude: true, Agent: { } agent } found
            ? await Deliver(new Target(team.Value.Team, name, found, agent), request.Text, cancellationToken).ConfigureAwait(false)
            : MetaErrors.ClaudeLane.With($"lane {name} runs Claude Code (or the launcher recorded no other agent for it): use SendMessage to {team.Value.Team}-{name}");
    }

    private async ValueTask<Result<LaneSendResponse>> Deliver(Target target, string text, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var seq = await mail.Send(target.Lane, text, now, cancellationToken).ConfigureAwait(false);
        if (seq.IsError)
        {
            return seq.Error;
        }

        var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"lane-send:{target.Lane}:{seq.Value}"));
        var sent = await messages.RecordOutbound(new Outbound(target.Session, text, now) { Id = id }, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        var attention = await Attention(target, cancellationToken).ConfigureAwait(false);
        if (attention.IsError)
        {
            return attention.Error;
        }

        var (woken, skipped) = attention.Value is null
            ? await Wake(target, now, cancellationToken).ConfigureAwait(false)
            : (false, "the lane's hooks can't take part, so the message waits");
        return new LaneSendResponse(target.Lane, id, woken, skipped, attention.Value);
    }

    // Why the lane can't act on the message, or null when its hooks have run since launch.
    private async ValueTask<Result<string?>> Attention(Target target, CancellationToken cancellationToken)
    {
        if (!target.Agent.Hooks)
        {
            return "launch-only: no obligations";
        }

        var beat = await beats.Last(target.Lane, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        return beat.Value is not { } last || target.Record.Session is null || (target.Record.RecordedAt is { } recorded && last.At < recorded)
            ? LaneHealthReader.HooksNotRunning(target.Agent)
            : Results.Success<string?>(value: null);
    }

    // The three guards: the lane is idle, its agent is up in the pane, and nobody is attached. Otherwise the message waits for its next hook.
    private async ValueTask<(bool Woken, string? Skipped)> Wake(Target target, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var idle = await beats.IsIdle(target.Lane, cancellationToken).ConfigureAwait(false);
        if (idle.IsError || !idle.Value)
        {
            return (false, "the lane is not idle: the message waits for its next hook");
        }

        var state = await pane.Probe(target.Session, cancellationToken).ConfigureAwait(false);
        if (state.IsError || state.Value is not { } found || !string.Equals(found.Command, target.Agent.ProcessName, StringComparison.Ordinal))
        {
            return (false, "lane down: no " + target.Agent.ProcessName + " in its pane");
        }

        if (found.Clients > 0)
        {
            return (false, "a client is attached: the message waits for its next hook");
        }

        var typed = await pane.Wake(target.Session, WakeLine, cancellationToken).ConfigureAwait(false);
        if (typed.IsError)
        {
            return (false, "the wake failed: " + typed.Error.Message.ReplaceLineEndings(" "));
        }

        _ = await beats.Woke(target.Lane, now, cancellationToken).ConfigureAwait(false);
        return (true, null);
    }
}
