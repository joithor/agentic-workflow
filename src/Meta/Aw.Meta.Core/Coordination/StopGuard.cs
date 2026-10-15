using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Guard;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Keeps the stop-check loop guard's state in the key-value store and logs an incident the first time it lets a stop through.</summary>
/// <param name="values">Holds the last open set and its block count.</param>
/// <param name="log">The running log.</param>
public sealed class StopGuard(IKeyValueStore values, IRunningLog log)
{
    /// <summary>Decides one stop-check and remembers the outcome.</summary>
    /// <param name="open">The open obligations.</param>
    /// <param name="hookActive">Whether the stop hook is already continuing a block.</param>
    /// <param name="max">How many identical blocks in a row are allowed.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Block or allow, with the warning when allowed with obligations open.</returns>
    public async ValueTask<Result<StopOutcome>> Decide(IReadOnlyList<Obligation> open, bool hookActive, int max, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var fingerprint = await values.Get(KvKeys.StopFingerprint, cancellationToken).ConfigureAwait(false);
        var count = await values.Get(KvKeys.StopCount, cancellationToken).ConfigureAwait(false);
        if (fingerprint.IsError || count.IsError)
        {
            return fingerprint.IsError ? fingerprint.Error : count.Error;
        }

        var previous = new GuardState(fingerprint.Value ?? string.Empty, int.TryParse(count.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0);
        var decision = StopLoopGuard.Decide(previous, StopLoopGuard.Fingerprint(open), hookActive, max);
        var saved = await Save(decision.State, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        if (!decision.Allowed)
        {
            return new StopOutcome(decision.Block, Warning: null);
        }

        var warning = string.Create(CultureInfo.InvariantCulture, $"aw-meta: stop allowed after {max} identical blocks; still open: {string.Join("; ", open.Select(o => o.Summary))}");
        var logged = decision.FirstAllowed
            ? await log.Append(LogLine.Event(LogKind.Incident, now, "stop-check loop guard let a stop through. " + warning), cancellationToken).ConfigureAwait(false)
            : Results.Ok();
        return logged.IsError ? logged.Error : new StopOutcome(Block: false, warning);
    }

    private async ValueTask<Result<Unit>> Save(GuardState state, CancellationToken cancellationToken)
    {
        var fingerprint = await values.Set(KvKeys.StopFingerprint, state.Fingerprint, cancellationToken).ConfigureAwait(false);
        return fingerprint.IsError
            ? fingerprint
            : await values.Set(KvKeys.StopCount, state.Count.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }
}
