using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The in-memory <see cref="ILaunchRequests"/> (UI spec E-19): the launches a test marks requested, read as the
/// open-request the engine sends for them (actor 'service:engine'). Passes <see cref="LaunchRequestsContract"/>.
/// </summary>
public sealed class InMemoryLaunchRequests : ILaunchRequests
{
    private readonly Dictionary<ItemKey, string> _requested = [];

    /// <summary>Marks <paramref name="launch"/> requested for <paramref name="item"/>, as API P10a's 'OpenItem' records it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="launch">The launch id ('LCH-…').</param>
    public void Request(ItemKey item, string launch) => _requested[item] = launch;

    /// <inheritdoc/>
    public ValueTask<Result<OpenRequested?>> Requested(ItemKey item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<OpenRequested?>>(_requested.TryGetValue(item, out var launch) ? new OpenRequested("service:engine", launch) : null);
}
