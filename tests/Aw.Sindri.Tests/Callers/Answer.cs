using System.Net;

namespace Aw.Sindri.Tests.Callers;

/// <summary>What a caller test reads from an answer.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Code">The ProblemDetails title (the SND code), or null.</param>
/// <param name="ApiVersion">The Sindri-Api-Version header, or null.</param>
/// <param name="Body">The body text.</param>
internal sealed record Answer(HttpStatusCode Status, string? Code, string? ApiVersion, string Body);
