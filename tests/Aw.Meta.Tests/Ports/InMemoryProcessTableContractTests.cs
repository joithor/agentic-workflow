using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryProcessTableContractTests : ProcessTableContract
{
    private readonly InMemoryProcessTable _table = new();

    protected override IProcessTable Create() => _table;

    protected override int StartProcess() => _table.Start();
}
