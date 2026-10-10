using Aw.Kernel.Testing;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The run-history contract bound to the in-memory fake (unit lane).</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRunHistoryContractTests : RunHistoryContract
{
    protected override IRunHistory NewHistory() => new InMemoryRunHistory();
}
