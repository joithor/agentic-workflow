using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The adapter until API P10a's launches projection: no launch is ever requested.</summary>
[Lane(TestLane.Unit)]
public sealed class NoLaunchRequestsContractTests : LaunchRequestsContract
{
    protected override ILaunchRequests Create() => NoLaunchRequests.Instance;

    protected override ILaunchRequests? CreateRequesting(ItemKey item, string launch) => null;   // the stub has no way to request one
}
