using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests.StopProcess;

/// <summary>An endpoint record that runs <see cref="BeforeRemove"/> before each removal, so a test can see what else holds then.</summary>
internal sealed class ObservedEndpointFile(IEndpointFile inner) : IEndpointFile
{
    public Action? BeforeRemove { get; set; }

    /// <summary>The token the last removal ran with.</summary>
    public CancellationToken? RemovedWith { get; private set; }

    public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken) => inner.Read(cancellationToken);

    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken) => inner.Write(endpoint, cancellationToken);

    public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken)
    {
        BeforeRemove?.Invoke();
        RemovedWith = cancellationToken;
        return inner.Remove(pid, cancellationToken);
    }
}
