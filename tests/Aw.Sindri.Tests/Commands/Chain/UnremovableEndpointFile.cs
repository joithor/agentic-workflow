using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>An endpoint record whose removal fails, as a read-only home would.</summary>
internal sealed class UnremovableEndpointFile : IEndpointFile
{
    internal const string Reason = "control.json couldn't be removed: read-only home";

    private readonly InMemoryEndpointFile _inner = new();

    public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken) => _inner.Read(cancellationToken);

    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken) => _inner.Write(endpoint, cancellationToken);

    public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(SindriErrors.LedgerUnavailable.With(Reason));
}
