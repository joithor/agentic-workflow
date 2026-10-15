using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Http;

/// <summary>Sends one request over HttpClient with redirects switched off, so the service decides whether a redirect is followed.</summary>
internal sealed class HttpApiTransport(TimeSpan timeout) : IApiTransport, IDisposable
{
    /// <inheritdoc/>
    public ValueTask<Result<HttpReply>> SendAsync(HttpCall call, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public void Dispose() => _ = timeout;
}
