using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Sends one HTTP request. It never follows a redirect: the service decides, so a redirect off the origin can be refused.</summary>
public interface IApiTransport
{
    /// <summary>Sends <paramref name="call"/>.</summary>
    /// <param name="call">The request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply (any status, a redirect included), or AW-EVIDENCE-023 when no reply came (connection, timeout).</returns>
    public ValueTask<Result<HttpReply>> SendAsync(HttpCall call, CancellationToken cancellationToken);
}
