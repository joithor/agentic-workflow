using System.Globalization;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>The state of one watch across its polls, and the lines each poll yields. Not shared between watches.</summary>
/// <param name="team">The team polled: its repo and its disk and memory floors.</param>
/// <param name="poller">Takes the snapshots.</param>
/// <param name="emit">Receives each line.</param>
internal sealed class WatchLoop(TeamConfig team, WatchPoller poller, Func<string, ValueTask> emit)
{
    private WatchState _state = WatchState.Initial;
    private int? _inbox;
    private bool _failing;

    /// <summary>How many lines were emitted.</summary>
    internal int Lines { get; private set; }

    /// <summary>Takes a snapshot and emits what changed. A failed poll is reported once per streak.</summary>
    internal async ValueTask Tick(CancellationToken cancellationToken)
    {
        var polled = await poller.Poll(team, cancellationToken).ConfigureAwait(false);
        if (polled.IsError)
        {
            if (!_failing)
            {
                await Say($"WATCH ERROR: {polled.Error.Code}: {polled.Error.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
            }

            _failing = true;
            return;
        }

        _failing = false;
        var step = WatchDiff.Step(_state, polled.Value.Snapshot);
        if (!_state.Initialized)
        {
            await Say(Started(polled.Value.Snapshot)).ConfigureAwait(false);
        }

        foreach (var line in step.Lines)
        {
            await Say(line).ConfigureAwait(false);
        }

        foreach (var line in polled.Value.Worktrees)
        {
            await Say(line).ConfigureAwait(false);
        }

        if (_inbox is { } before && polled.Value.Inbox > before)
        {
            await Say(string.Create(CultureInfo.InvariantCulture, $"INBOX {polled.Value.Inbox - before} new message(s)")).ConfigureAwait(false);
        }

        _inbox = polled.Value.Inbox;
        _state = step.State;
    }

    /// <summary>Emits one line.</summary>
    internal async ValueTask Say(string line)
    {
        Lines++;
        await emit(line).ConfigureAwait(false);
    }

    private static string Started(WatchSnapshot snapshot) =>
        string.Create(CultureInfo.InvariantCulture, $"WATCH started: {snapshot.Prs.Count(pr => pr.State == PrState.Open)} open PR(s), main {(snapshot.MainSha.Length > 7 ? snapshot.MainSha[..7] : snapshot.MainSha)}");
}
