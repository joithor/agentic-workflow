using Aw.Kernel.Testing;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaneFilesContractTests : LaneFilesContract
{
    protected override ValueTask<(ILaneFiles Files, Func<LaneRecord, Task> Put)> Create()
    {
        var files = new InMemoryLaneFiles();
        return ValueTask.FromResult<(ILaneFiles, Func<LaneRecord, Task>)>((files, record => { files.Put(record); return Task.CompletedTask; }));
    }
}
