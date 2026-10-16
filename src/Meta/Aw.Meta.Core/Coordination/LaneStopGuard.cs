using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Guard;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// The lane's stop gate with the same loop guard as the meta's stop-check: a check that is not a continuation blocks and resets the
/// count; each continuation with the same owed set counts one block, and after the limit the next is let through with a warning, an
/// incident in the running log, and a mark that health shows as "obligation not delivered (stop cap)".
/// </summary>
/// <param name="values">Holds each lane's last owed set and its block count.</param>
/// <param name="log">The running log.</param>
public sealed class LaneStopGuard(IKeyValueStore values, IRunningLog log)
{
    /// <summary>Decides one stop-check for a lane and remembers the outcome.</summary>
    /// <param name="stop">The lane, what it owes, and the hook's state.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Block or allow, with the warning when allowed with a reply owed.</returns>
    public async ValueTask<Result<StopOutcome>> Decide(LaneStop stop, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stop);
        var lane = stop.Identity.Lane;
        var fingerprint = await values.Get(Key(lane, "stop-fingerprint"), cancellationToken).ConfigureAwait(false);
        var count = await values.Get(Key(lane, "stop-count"), cancellationToken).ConfigureAwait(false);
        if (fingerprint.IsError || count.IsError)
        {
            return fingerprint.IsError ? fingerprint.Error : count.Error;
        }

        var previous = new GuardState(fingerprint.Value ?? string.Empty, int.TryParse(count.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0);
        var decision = StopLoopGuard.Decide(previous, Fingerprint(stop.Owed), stop.HookActive, stop.MaxBlocks);
        var saved = await Save(lane, decision.State, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        return decision.Allowed
            ? await LetThrough(stop, decision, cancellationToken).ConfigureAwait(false)
            : new StopOutcome(decision.Block, Warning: null);
    }

    // The cap: a warning for the hook's stderr, the mark health reads, and (the first time) an incident in the running log.
    private async ValueTask<Result<StopOutcome>> LetThrough(LaneStop stop, GuardDecision decision, CancellationToken cancellationToken)
    {
        var max = stop.MaxBlocks.ToString(CultureInfo.InvariantCulture);
        var warning = $"aw-meta: stop allowed after {max} identical blocks; lane {stop.Identity.Lane} still owes the meta a reply";
        var marked = await values.Set(Key(stop.Identity.Lane, "stop-cap"), decision.State.Fingerprint, cancellationToken).ConfigureAwait(false);
        var logged = marked.IsSuccess && decision.FirstAllowed
            ? await log.Append(LogLine.Event(LogKind.Incident, stop.Now, $"lane {stop.Identity.SessionName}: stop let through after {max} blocks, reply still owed to the meta"), cancellationToken).ConfigureAwait(false)
            : marked;
        return logged.IsError ? logged.Error : new StopOutcome(Block: false, warning);
    }

    /// <summary>The block count a lane's stop gate has reached for its current owed set.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="owed">The messages the lane owes a reply to now.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The blocks so far, and whether the stop cap already let this owed set through.</returns>
    public async ValueTask<Result<(int Blocks, bool Capped)>> State(string lane, IReadOnlyList<LaneMessage> owed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owed);
        var fingerprint = await values.Get(Key(lane, "stop-fingerprint"), cancellationToken).ConfigureAwait(false);
        var count = await values.Get(Key(lane, "stop-count"), cancellationToken).ConfigureAwait(false);
        var cap = await values.Get(Key(lane, "stop-cap"), cancellationToken).ConfigureAwait(false);
        if (fingerprint.IsError || count.IsError || cap.IsError)
        {
            return fingerprint.IsError ? fingerprint.Error : count.IsError ? count.Error : cap.Error;
        }

        var current = Fingerprint(owed);
        var same = current.Length > 0 && string.Equals(fingerprint.Value, current, StringComparison.Ordinal);
        var blocks = same && int.TryParse(count.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return (blocks, current.Length > 0 && string.Equals(cap.Value, current, StringComparison.Ordinal));
    }

    private static string Fingerprint(IReadOnlyList<LaneMessage> owed) =>
        StopLoopGuard.Fingerprint([.. owed.Select(m => new Obligation(ObligationKind.Reply, m.Seq.ToString(CultureInfo.InvariantCulture), m.Text, string.Empty))]);

    private async ValueTask<Result<Unit>> Save(string lane, GuardState state, CancellationToken cancellationToken)
    {
        var fingerprint = await values.Set(Key(lane, "stop-fingerprint"), state.Fingerprint, cancellationToken).ConfigureAwait(false);
        return fingerprint.IsError
            ? fingerprint
            : await values.Set(Key(lane, "stop-count"), state.Count.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }

    private static string Key(string lane, string name) => "lane." + lane + "." + name;
}
