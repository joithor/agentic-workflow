using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaunchRequestsContractTests : LaunchRequestsContract
{
    protected override ILaunchRequests Create() => new InMemoryLaunchRequests();

    [Fact]
    public async Task A_requested_launch_reads_as_the_engine_s_open_request()
    {
        var launches = new InMemoryLaunchRequests();
        launches.Request(ItemKey.From("T-2"), "LCH-0123456789abcdef0123456789abcdef");

        var read = await launches.Requested(ItemKey.From("T-2"), TestContext.Current.CancellationToken);

        Assert.Equal(("service:engine", "LCH-0123456789abcdef0123456789abcdef"), (read.Value?.Actor, read.Value?.Launch));
    }
}
