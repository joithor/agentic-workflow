using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Runs the vetted requests in order against the host, follows same-origin redirects, and checks each expectation.</summary>
internal sealed class RequestRunner(IApiTransport transport)
{
    internal IApiTransport Transport { get; } = transport;

    internal ValueTask<Collected> RunAsync(VettedRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
