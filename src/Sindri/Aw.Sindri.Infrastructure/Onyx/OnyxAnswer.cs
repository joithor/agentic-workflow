using System.Net;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>What Onyx answered one provisioning request: the status and the body text.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The body (may hold a secret: never log it).</param>
internal sealed record OnyxAnswer(HttpStatusCode Status, string Body)
{
    /// <summary>Gets a value indicating whether the status is 2xx.</summary>
    public bool Success => (int)Status is >= 200 and < 300;
}
