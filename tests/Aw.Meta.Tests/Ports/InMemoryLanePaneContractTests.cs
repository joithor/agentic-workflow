using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLanePaneContractTests : LanePaneContract
{
    protected override ValueTask<(ILanePane Pane, Action<string, string, int> Start)> Create()
    {
        var pane = new InMemoryLanePane();
        return ValueTask.FromResult<(ILanePane, Action<string, string, int>)>((pane, pane.Put));
    }
}
