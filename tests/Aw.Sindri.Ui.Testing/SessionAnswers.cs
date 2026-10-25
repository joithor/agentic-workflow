using System.Net;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// What the API answers on the session reads (collector spec CA-1, CA-2), taken from its goldens on disk. The list is the three-provider
/// fixture. A session is the view golden that names its ref (the "session caller" golden is the same ref with fields hidden, so it is
/// served only when a test asks for it), else a view built from the list's row (no cwd, no history); anything else is the 404 the API writes.
/// </summary>
internal static class SessionAnswers
{
    private const string Fixture = "fixture-three-providers";
    private const string Hidden = "session-caller-hidden-fields";

    /// <summary>GET /sessions.</summary>
    /// <returns>The answer.</returns>
    internal static RouteAnswer List() => RouteAnswer.Api("sessions", Fixture);

    /// <summary>GET /sessions/{provider}/{id}.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="id">The session id.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer One(string provider, string id)
    {
        var reference = provider + "/" + id;
        var served = GoldenFiles.All()
            .Where(g => g.Kind == "Api" && g.Slug == "sessions-id" && g.Scenario != Hidden)
            .Select(g => JsonNode.Parse(g.Body)!) // why: the goldens are JSON documents
            .FirstOrDefault(b => b["traceId"] is null && string.Equals((string?)b["session"]?["ref"], reference, StringComparison.Ordinal));
        if (served is not null)
        {
            return new RouteAnswer(HttpStatusCode.OK, served.ToJsonString());
        }

        var list = JsonNode.Parse(List().Body)!; // why: the golden is a JSON document
        var row = list["sessions"]!.AsArray().FirstOrDefault(r => string.Equals((string?)r!["ref"], reference, StringComparison.Ordinal)); // why: the golden has rows
        return row is null
            ? new RouteAnswer(HttpStatusCode.NotFound, GoldenFiles.Api("sessions-id", "not-found"))
            : new RouteAnswer(HttpStatusCode.OK, new JsonObject { ["session"] = row.DeepClone(), ["cwd"] = null, ["stateHistory"] = null, ["asOf"] = list["asOf"]!.DeepClone() }.ToJsonString());
    }
}
