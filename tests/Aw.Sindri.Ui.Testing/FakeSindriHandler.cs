using System.Globalization;
using System.Net;
using System.Text;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// Serves a <see cref="FakeSindriApi"/> over HTTP the way Sindri's API writes it (docs/csharp/sindri.md: camelCase JSON,
/// server-sent events with id = sequence and event = type), so the real client runs against the same model as the fake.
/// The JSON is written by hand here, not by the app's own context, so a field-name mistake in the app shows.
/// </summary>
/// <param name="server">The model.</param>
internal sealed class FakeSindriHandler(FakeSindriApi server) : HttpMessageHandler
{
    /// <summary>The requests seen, in order: the path and the headers the app's identity rests on (API §6).</summary>
    public IReadOnlyList<SeenRequest> Requests => [.. _requests];

    private readonly List<SeenRequest> _requests = [];

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
            "/sindri/ping" => Text(HttpStatusCode.OK, string.Create(CultureInfo.InvariantCulture, $$"""{"pid":{{server.Pong.Pid}},"epoch":{{server.Pong.Epoch}}}"""), "application/json"),
            "/sindri/status" => Text(HttpStatusCode.OK, Status((await server.Status(uri, cancellationToken)).Value), "application/json"),
            "/sindri/events" => Events(After(uri), cancellationToken),
            _ => Text(HttpStatusCode.NotFound, "{}", "application/json"),
        };
    }

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

    private static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"sequence":{{e.Sequence}},"type":"{{e.Type}}","occurredAt":"{{Time(e.OccurredAt)}}","actor":"sindri","tenant":"local","ring":0,"epoch":7,"payload":"{}"}""");

    private static string Status(StatusView s) => string.Create(CultureInfo.InvariantCulture,
        $$"""{"running":{{Bool(s.Running)}},"pid":{{s.Pid}},"epoch":{{s.Epoch}},"leaseExpiresAt":{{Quoted(s.LeaseExpiresAt)}},"profile":{"approved":{{Quoted(s.Profile.Approved)}},"live":{{Quoted(s.Profile.Live)}},"changedSinceApproval":{{Bool(s.Profile.ChangedSinceApproval)}},"problem":{{Quoted(s.Profile.Problem)}},"source":{{Quoted(s.Profile.Source)}},"needsApproval":{{Bool(s.Profile.NeedsApproval)}}},"services":[{{string.Join(',', s.Services.Select(Service))}}]{{ApiVersion(s.ApiVersion)}}}""");

    // Absent before API P1 (the field doesn't exist on S1's status), so the app must read a status without it.
    private static string ApiVersion(int? version) => version is { } v ? string.Create(CultureInfo.InvariantCulture, $",\"apiVersion\":{v}") : string.Empty;

    private static string Service(ServiceView v) => $$"""{"name":"{{v.Name}}","every":"{{v.Every}}","lastRun":{{Quoted(v.LastRun)}}}""";

    private static string Bool(bool b) => b ? "true" : "false";

    private static string Quoted(string? s) => s is null ? "null" : $"\"{s}\"";

    private static string Quoted(DateTimeOffset? t) => t is { } v ? $"\"{Time(v)}\"" : "null";

    private static string Time(DateTimeOffset t) => t.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
