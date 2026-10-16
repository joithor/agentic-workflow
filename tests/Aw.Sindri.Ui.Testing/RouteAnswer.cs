using System.Net;

namespace Aw.Sindri.Ui.Testing;

/// <summary>What the fake answers on one route: an HTTP status and a body as served (a golden, or a ProblemDetails refusal).</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The JSON body.</param>
internal sealed record RouteAnswer(HttpStatusCode Status, string Body)
{
    /// <summary>A 200 with an API golden.</summary>
    internal static RouteAnswer Api(string slug, string scenario) => new(HttpStatusCode.OK, GoldenFiles.Api(slug, scenario));

    /// <summary>A 200 with a spec-shaped body.</summary>
    internal static RouteAnswer SpecShaped(string slug, string scenario) => new(HttpStatusCode.OK, GoldenFiles.SpecShaped(slug, scenario));

    /// <summary>A refusal as the API writes one (title = the SND code, detail = the message with its fix).</summary>
    internal static RouteAnswer Problem(HttpStatusCode status, string code, string detail) =>
        new(status, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $$"""{"type":"about:blank","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}","traceId":"<trace>"}"""));

    /// <summary>True for a 2xx answer.</summary>
    // A spec-shaped ProblemDetails golden, served with the status it names.
    internal static RouteAnswer Problem(string slug, string scenario)
    {
        var body = GoldenFiles.SpecShaped(slug, scenario);
        var status = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32();
        return new((HttpStatusCode)status, body);
    }

    internal bool IsSuccess => (int)Status is >= 200 and < 300;
}
