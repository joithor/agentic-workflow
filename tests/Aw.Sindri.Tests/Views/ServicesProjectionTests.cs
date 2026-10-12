using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'services' fold (API spec §2.3): 'service.ran' → the service's row with its last run.</summary>
public sealed class ServicesProjectionTests
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public void Services_fold_keeps_the_last_run()
    {
        var slot = _h.Clock.GetUtcNow();
        var first = Ran(slot, ServiceOutcome.Ok, code: null, sequence: 3);
        _h.Clock.Advance(TimeSpan.FromSeconds(30));
        var second = Ran(slot.AddSeconds(30), ServiceOutcome.Ok, code: null, sequence: 7);

        var row = ServicesProjection.Fold(ServicesProjection.Fold(row: null, Payload(first), first), Payload(second), second);

        Assert.Equal(new ServiceRow(_heartbeat, new LastRun(slot.AddSeconds(30), ServiceOutcome.Ok, Code: null, second.Envelope.OccurredAt), Paused: false, 7), row);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failure_then_a_success_reads_ok()
    {
        var slot = _h.Clock.GetUtcNow();
        var failed = Ran(slot, ServiceOutcome.Failed, "SND-SCHED-001", sequence: 1);
        var ok = Ran(slot.AddSeconds(5), ServiceOutcome.Ok, code: null, sequence: 2);

        var afterFailure = ServicesProjection.Fold(row: null, Payload(failed), failed);
        var afterSuccess = ServicesProjection.Fold(afterFailure, Payload(ok), ok);

        Assert.Equal((ServiceOutcome.Failed, "SND-SCHED-001"), (afterFailure.LastRun!.Outcome, afterFailure.LastRun.Code));
        Assert.Equal(ServiceOutcome.Ok, afterSuccess.LastRun!.Outcome);
        Assert.Null(afterSuccess.LastRun.Code);
    }

    private Core.Ledger.LedgerEvent Ran(DateTimeOffset slot, ServiceOutcome outcome, string? code, long sequence) =>
        Recorded.Of(_h.Events, EventTypes.ServiceRan, new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, slot, outcome, code), sequence);

    private ServiceRan Payload(Core.Ledger.LedgerEvent recorded) => _h.Events.Payload<ServiceRan>(recorded).Value;
}
