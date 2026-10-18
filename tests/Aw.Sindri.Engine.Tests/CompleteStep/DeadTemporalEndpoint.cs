using Aw.Kernel;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>A control.json that records the dev server on one port.</summary>
internal sealed class DeadTemporalEndpoint(int port) : IEndpointFile
{
    public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationct) =>
        ValueTask.FromResult<Result<ControlEndpoint?>>(new ControlEndpoint(new Uri("http://127.0.0.1:1"), Pid: 1, Epoch: 1) { Temporal = new TemporalEndpoint(Pid: 1, port, UiPort: 0) });

    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationct) => throw new NotSupportedException("read only");

    public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationct) => throw new NotSupportedException("read only");
}
