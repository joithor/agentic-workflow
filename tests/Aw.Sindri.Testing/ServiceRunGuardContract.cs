using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract of the guard that makes a scheduled run and a manual run of one service exclusive and shares their last outcome (API P6 plan 1.5):
/// one claim per service at a time, independent services, a released claim can be taken again, and a recorded outcome is what the scheduler reads next.
/// </summary>
public abstract class ServiceRunGuardContract
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private static readonly ServiceName _other = ServiceName.From("other");

    /// <summary>A fresh guard.</summary>
    /// <returns>The guard.</returns>
    protected abstract ServiceRunGuard Create();

    /// <summary>A claimed service refuses a second claim until the first is released; releasing twice is harmless.</summary>
    [Fact]
    public void A_service_has_one_run_at_a_time()
    {
        var guard = Create();

        Expect.True(guard.TryClaim(_heartbeat, out var first));
        Expect.True(!guard.TryClaim(_heartbeat, out _));
        first.Dispose();
        first.Dispose();
        Expect.True(guard.TryClaim(_heartbeat, out var again));
        again.Dispose();
    }

    /// <summary>A manual claim is told from a scheduled one while it is held, and neither is left behind after the release.</summary>
    [Fact]
    public void A_manual_run_is_told_from_a_scheduled_one()
    {
        var guard = Create();

        Expect.True(guard.TryClaimManual(_heartbeat, out var manual));
        Expect.True(guard.IsManualRun(_heartbeat));
        manual.Dispose();
        Expect.True(!guard.IsManualRun(_heartbeat));
        Expect.True(guard.TryClaim(_heartbeat, out var scheduled));
        Expect.True(!guard.IsManualRun(_heartbeat));
        scheduled.Dispose();
    }

    /// <summary>Claims of different services don't touch.</summary>
    [Fact]
    public void Different_services_claim_independently()
    {
        var guard = Create();

        Expect.True(guard.TryClaim(_heartbeat, out var first));
        Expect.True(guard.TryClaim(_other, out var second));
        first.Dispose();
        second.Dispose();
    }

    /// <summary>The outcome is unknown until a run records it, then it is the latest one, for any claimant (a manual run's outcome is the scheduler's next 'last').</summary>
    [Fact]
    public void A_recorded_outcome_is_the_last_one_for_everyone()
    {
        var guard = Create();
        Expect.True(guard.LastOutcome(_heartbeat) is null);

        guard.Record(_heartbeat, ServiceOutcome.Failed);
        Expect.Equal(ServiceOutcome.Failed, guard.LastOutcome(_heartbeat));
        guard.Record(_heartbeat, ServiceOutcome.Ok);
        Expect.Equal(ServiceOutcome.Ok, guard.LastOutcome(_heartbeat));
        Expect.True(guard.LastOutcome(_other) is null);

        guard.Forget(_heartbeat);
        Expect.True(guard.LastOutcome(_heartbeat) is null);
    }
}
