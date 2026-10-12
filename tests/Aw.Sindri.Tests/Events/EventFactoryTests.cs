using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Events;

public sealed class EventFactoryTests
{
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public void Entry_takes_the_actor_and_correlation_from_the_caller()
    {
        var caller = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

        using (_h.Callers.Enter(caller))
        {
            var envelope = _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k").Envelope;

            Assert.Equal("human:cli", envelope.Actor);
            Assert.Equal(caller.CorrelationId.Value, envelope.CorrelationId);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Entry_without_a_caller_keeps_host_sindri_and_a_fresh_correlation()
    {
        var first = _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k1").Envelope;
        var second = _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k2").Envelope;

        Assert.Equal("host:sindri", first.Actor);
        Assert.NotEqual(first.CorrelationId, second.CorrelationId);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entry_sets_the_item_when_given()
    {
        Assert.Equal("ITM-1", _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k", "ITM-1").Item);
        Assert.Null(_h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k").Item);
    }
}
