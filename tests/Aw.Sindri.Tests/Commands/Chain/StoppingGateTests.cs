using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.Commands;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The stop's decorator cancels the gate's holder only once the stop has stopped the process.</summary>
public sealed class StoppingGateTests
{
    // A stop that fails (its ledger append, a cancelled request) leaves a process that keeps serving, so its gate must keep working.
    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_that_fails_leaves_the_gate_working()
    {
        using var gate = new CommandGateLock();

        var stopped = await new StoppingGate<FixtureCommand, CommandAccepted>(new Failing(), gate).Handle(new FixtureCommand("accept"), TestContext.Current.CancellationToken);

        Assert.True(stopped.IsError);
        Assert.False(gate.Stopping.IsCancellationRequested);
    }

    private sealed class Failing : IService<FixtureCommand, CommandAccepted>
    {
        public ValueTask<Result<CommandAccepted>> Handle(FixtureCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<CommandAccepted>>(SindriErrors.InvalidInput.With("the stop failed"));
    }
}
