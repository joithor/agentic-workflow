using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The adapter over the 'launches' view honours the port's contract. Its launches are item ids ('ITM-…'), so the contract's loose item key can't be requested.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchRequestsViewContractTests : LaunchRequestsContract
{
    protected override ILaunchRequests Create() => new LaunchRequestsView();

    protected override ILaunchRequests? CreateRequesting(ItemKey item, LaunchId launch) => null;   // the view holds item ids; 'LaunchRequestsViewTests' covers a request
}
