using System.Net;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx's answer to one call: the status and the body bytes.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The body.</param>
internal sealed record OnyxReply(HttpStatusCode Status, byte[] Body);
