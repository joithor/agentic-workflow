namespace Aw.Evidence.Core.Data;

/// <summary>What a request got back.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Location">The Location header of a redirect, else null.</param>
/// <param name="Body">The body as text.</param>
public sealed record HttpReply(int Status, string? Location, string Body);
