using System.Net;

namespace Aw.Sindri.Tests.Commands;

/// <summary>One canned answer of <see cref="JobApi"/>.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The body.</param>
internal sealed record Reply(HttpStatusCode Status, string Body);
