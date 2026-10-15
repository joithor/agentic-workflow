using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryRunningLogContractTests : RunningLogContract
{
    protected override IRunningLog Create() => new InMemoryRunningLog();

    protected override IReadOnlyList<string> Read(IRunningLog log) => ((InMemoryRunningLog)log).Lines;
}
