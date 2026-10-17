using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The adapter over the 'launches' view (U9 red).</summary>
internal sealed class LaunchRequestsView : ILaunchRequests
{
    public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LaunchId?>>((LaunchId?)null);   // U9 red: reads nothing yet
}
