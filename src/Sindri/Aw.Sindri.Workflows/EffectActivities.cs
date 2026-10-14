using Aw.Sindri.Core.Ledger;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Effects are activities (engine spec rev 8 §5.7; engine P6). After a transition's ledger event the interpreter, for
/// each effect: writes its 'outbox.pending' row ('RecordEffect', a local activity keyed like 'RecordEvent'), waits
/// 'workflow.dispatchDelay' on a workflow timer (cancelled with the instance), rechecks the recipe's approval and the
/// profile's allowlist ('RecheckEffect', a local activity), then either runs it ('RunEffect', a regular activity that
/// calls the block's 'IEffectKind', writes 'outbox.dispatched' and appends the reply event caused by that row) or
/// writes 'outbox.refused' and hands the instance an 'OutboxRefused' trigger. Kernel's 'outbox_commands' is not used.
/// </summary>
internal sealed class EffectActivities
{
    /// <summary>'RecordEffect' and 'RecheckEffect' run as local activities that retry without bound (the ledger must have the row).</summary>
    public static LocalActivityOptions LocalOptions => RecordEventActivities.LocalOptions;

    /// <summary>
    /// 'RunEffect' runs as a regular activity: retried with backoff, since the block's existence check makes a repeat
    /// harmless. Engine P6 unit (a) sets the timeouts.
    /// </summary>
    public static ActivityOptions RunOptions { get; } = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2, MaximumInterval = TimeSpan.FromMinutes(1), MaximumAttempts = 0 },
    };

    /// <summary>Appends <paramref name="record"/>'s row under 'workflow:{workflow id}:{run id}:{activity id}'. Engine P6 unit (a).</summary>
    /// <param name="record">The row.</param>
    /// <returns>Its ledger position.</returns>
    [Activity("RecordEffect")]
    public Task<LedgerAppend> RecordAsync(EffectRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        throw Unbuilt("unit (a): the RecordEffect activity (an effect's ledger row)");
    }

    /// <summary>The recheck: the recipe's approval and the approved profile's allowlist, now. Engine P6 unit (b).</summary>
    /// <param name="recheck">The recipe, the effect and its origin.</param>
    /// <returns>Null, or the refusal's code.</returns>
    [Activity("RecheckEffect")]
    public Task<EffectRecheckResult> RecheckAsync(EffectRecheck recheck)
    {
        ArgumentNullException.ThrowIfNull(recheck);
        throw Unbuilt("unit (b): the RecheckEffect local activity");
    }

    /// <summary>Performs the effect through its block, writes 'outbox.dispatched' and appends its reply. Engine P6 unit (a).</summary>
    /// <param name="run">The effect and its emission.</param>
    /// <returns>The dispatched row and the reply.</returns>
    [Activity("RunEffect")]
    public Task<EffectRunResult> RunAsync(EffectRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        throw Unbuilt("unit (a): the RunEffect activity");
    }

    private NotSupportedException Unbuilt(string what) => new($"engine P6 {what} is not built yet ({GetType().Name})");
}
