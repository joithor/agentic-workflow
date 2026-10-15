using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Stub <see cref="ILaunchRequests"/> until API P10a's launches projection lands (UI spec E-1, E-19): no launch is ever
/// requested, so every start runs as it did. API P10a's adapter over the 'launches' view replaces it. Engine P5 unit (b).
/// </summary>
internal sealed class NoLaunchRequests : ILaunchRequests
{
    /// <summary>The one instance.</summary>
    public static NoLaunchRequests Instance { get; } = new();

    public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LaunchId?>>((LaunchId?)null);
}
