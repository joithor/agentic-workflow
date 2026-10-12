using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The host tests' worker against the port's contract (its Fault option is the one deliberate breach, for serve's guard).</summary>
[Lane(TestLane.Unit)]
public sealed class ScriptedWorkerContractTests : TemporalWorkerContract
{
    protected override ValueTask<(ITemporalWorker Worker, TemporalEndpoint Frontend)> Create() =>
        ValueTask.FromResult<(ITemporalWorker, TemporalEndpoint)>((new ScriptedWorker(), new TemporalEndpoint(1, 7233, 8233)));
}
