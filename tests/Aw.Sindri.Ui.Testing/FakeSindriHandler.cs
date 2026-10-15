using System.Globalization;
using System.Net;
using System.Text;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// Serves a <see cref="FakeSindriApi"/> over HTTP the way Sindri's API writes it (docs/csharp/sindri.md: camelCase JSON,
/// server-sent events with id = sequence and event = type), so the real client runs against the same model as the fake.
/// The JSON is written by hand here, not by the app's own context, so a field-name mistake in the app shows; the
/// refusals are Sindri's own goldens served verbatim, and GoldenContractTests holds the hand-written bodies to the
/// goldens' shape (UI spec E-17).
/// </summary>
/// <param name="server">The model.</param>
internal sealed class FakeSindriHandler(FakeSindriApi server) : HttpMessageHandler
{
    /// <summary>The requests seen, in order: the path and the headers the app's identity rests on (API §6).</summary>
    public IReadOnlyList<SeenRequest> Requests => [.. _requests];

    private readonly List<SeenRequest> _requests = [];

    /// <summary>When set, the token Sindri holds: a request without it is refused with 403 SND-AUTH-001, as Sindri's caller check does.</summary>
    public string? RequiredToken { get; set; }

    /// <summary>When set, every route answers this status with this body instead.</summary>
    public (HttpStatusCode Code, string Body)? Override { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(new SeenRequest(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                Header(request, "X-Sindri-Caller"),
                Header(request, "X-Sindri-Client"),
                Header(request, "X-Sindri-Terminal"),
                Header(request, "Origin"),
                Header(request, "X-Sindri-Token")));
        }

        if (server.Down)
        {
            throw new HttpRequestException("Connection refused (127.0.0.1)");
        }

        if (Refused(request) is { } refused)
        {
            return refused;
        }

        if (Override is { } o)
        {
            return Text(o.Code, o.Body, "application/json");
        }

        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        if (server.FailPing && uri.AbsolutePath == "/sindri/ping")
        {
            throw new HttpRequestException("Connection refused (127.0.0.1)");
        }

        return uri.AbsolutePath switch
        {
            "/sindri/ping" => Text(HttpStatusCode.OK, Ping(server.Pong), "application/json"),
            "/sindri/status" => Text(HttpStatusCode.OK, Status((await server.Status(uri, cancellationToken)).Value), "application/json"),
            "/sindri/events" => Events(After(uri), cancellationToken),
            _ => Text(HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }

    // Sindri's caller check (a human without this start's token), then the model's own refusal: 403 with ProblemDetails.
    private HttpResponseMessage? Refused(HttpRequestMessage request)
    {
        var sent = Header(request, "X-Sindri-Token");
        if (RequiredToken is { } held && !string.Equals(held, sent, StringComparison.Ordinal))
        {
            return Answer(new RouteAnswer(HttpStatusCode.Forbidden, GoldenFiles.Api("status", sent is null ? "human-no-token" : "human-stale-token")));
        }

        return server.Refusal is { } refusal ? Answer(RouteAnswer.Problem(HttpStatusCode.Forbidden, refusal.Code, refusal.Detail)) : null;
    }

    private static HttpResponseMessage Answer(RouteAnswer answer) => Text(answer.Status, answer.Body, "application/problem+json");

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    private static long After(Uri uri) =>
        uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=')).Where(p => p is ["after", _]).Select(p => long.Parse(p[1], CultureInfo.InvariantCulture)).FirstOrDefault();

    private HttpResponseMessage Events(long after, CancellationToken cancellationToken)
    {
        var body = new ChunkStream();
        _ = Task.Run(async () =>
        {
            await foreach (var e in server.Events(new Uri("http://fake/"), after, cancellationToken))
            {
                body.Send(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"id: {e.Sequence}\nevent: {e.Type}\ndata: {Event(e)}\n\n")));
            }

            body.End();
        }, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) { Headers = { { "Content-Type", "text/event-stream" } } } };
    }

    private static HttpResponseMessage Text(HttpStatusCode code, string body, string type) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, type) };

    /// <summary>A ping body as this handler writes it.</summary>
    internal static string Ping(PingView p) => string.Create(CultureInfo.InvariantCulture, $$"""{"pid":{{p.Pid}},"epoch":{{p.Epoch}}}""");

    /// <summary>An event's data line as this handler writes it.</summary>
    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"sequence":{{e.Sequence}},"type":"{{e.Type}}","occurredAt":"{{Time(e.OccurredAt)}}","actor":"sindri","tenant":"local","ring":0,"epoch":7,"payload":"{}"}""");

    /// <summary>A status body as this handler writes it.</summary>
    internal static string Status(StatusView s) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"running":{{Bool(s.Running)}},"pid":{{s.Pid}},"epoch":{{s.Epoch}},"leaseExpiresAt":{{Quoted(s.LeaseExpiresAt)}},"profile":{"approved":{{Quoted(s.Profile.Approved)}},"live":{{Quoted(s.Profile.Live)}},"changedSinceApproval":{{Bool(s.Profile.ChangedSinceApproval)}},"problem":{{Quoted(s.Profile.Problem)}},"source":{{Quoted(s.Profile.Source)}},"needsApproval":{{Bool(s.Profile.NeedsApproval)}}},"services":[{{string.Join(',', s.Services.Select(Service))}}]{{ApiVersion(s.ApiVersion)}}{{ViewsAndHealth(s.Profile.NeedsApproval)}}}""");

    // Absent before API P1 (the field doesn't exist on S1's status), so the app must read a status without it.
    private static string ApiVersion(int? version) => version is { } v ? string.Create(CultureInfo.InvariantCulture, $",\"apiVersion\":{v}") : string.Empty;

    // API P2 adds views and health to /status. The fake serves fresh views and the health the profile implies.
    private static string ViewsAndHealth(bool needsApproval) => needsApproval
        ? $$""",{{FreshViews}},"health":{"word":"degraded","problems":[{"code":"SND-PROFILE-005","text":"Not approved: run `sindri profile approve`","clause":"the profile isn't approved"}],"sentence":"Sindri is degraded: the profile isn't approved."}"""
        : $$""",{{FreshViews}},"health":{"word":"healthy","problems":[],"sentence":null}""";

    private const string FreshViews = "\"views\":{\"cursor\":0,\"ledgerHead\":0,\"projectedAt\":null,\"rebuilding\":false,\"stalled\":false,\"stalledAt\":null,\"stalledType\":null,\"text\":null}";

    private static string Service(ServiceView v) => $$"""{"name":"{{v.Name}}","every":"{{v.Every}}","lastRun":{{Quoted(v.LastRun)}}}""";

    private static string Bool(bool b) => b ? "true" : "false";

    private static string Quoted(string? s) => s is null ? "null" : $"\"{s}\"";

    private static string Quoted(DateTimeOffset? t) => t is { } v ? $"\"{Time(v)}\"" : "null";

    private static string Time(DateTimeOffset t) => t.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
