using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IApiTransport contract: any status comes back as a reply (never an error), the body and the Location of a redirect
/// read back, a redirect is never followed, a body and headers are sent, and a host nothing answers on is AW-EVIDENCE-023.
/// </summary>
public abstract class ApiTransportContract
{
    /// <summary>Arranges a server that answers the given routes (by path) and returns the transport and the server's origin.</summary>
    /// <param name="routes">'METHOD path' to the reply the server gives; a path not listed is a 404.</param>
    /// <returns>The transport and the origin its calls go to.</returns>
    protected abstract Task<ApiUnderTest> ArrangeAsync(IReadOnlyDictionary<string, HttpReply> routes);

    /// <summary>The origin of a host nothing listens on.</summary>
    /// <returns>An origin, e.g. 'http://127.0.0.1:1'.</returns>
    protected abstract Task<ApiUnderTest> ArrangeUnreachableAsync();

    /// <summary>A 200 with a body reads back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reply_reads_back_with_its_status_and_body()
    {
        var api = await ArrangeAsync(Routes(("GET /notes", new HttpReply(200, Location: null, """{"n":1}"""))));

        var reply = await api.Transport.SendAsync(Call(HttpVerb.Get, api.Origin + "/notes"), CancellationToken.None);

        Expect.That(reply.IsSuccess && reply.Value.Status == 200 && reply.Value.Body == """{"n":1}""", "the status and body read back");
    }

    /// <summary>An error status is a reply, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_error_status_is_a_reply_not_an_error()
    {
        var api = await ArrangeAsync(Routes());

        var reply = await api.Transport.SendAsync(Call(HttpVerb.Get, api.Origin + "/missing"), CancellationToken.None);

        Expect.That(reply.IsSuccess && reply.Value.Status == 404, "a 404 comes back as a reply");
    }

    /// <summary>A redirect comes back with its Location and is not followed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_redirect_is_returned_with_its_location_and_never_followed()
    {
        var api = await ArrangeAsync(Routes(("GET /old", new HttpReply(302, "/new", string.Empty)), ("GET /new", new HttpReply(200, Location: null, "arrived"))));

        var reply = await api.Transport.SendAsync(Call(HttpVerb.Get, api.Origin + "/old"), CancellationToken.None);

        Expect.That(reply.IsSuccess && reply.Value.Status == 302 && reply.Value.Location == "/new", "the 302 and its Location come back, unfollowed");
    }

    /// <summary>A body is sent to the server.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_post_body_reaches_the_server()
    {
        var api = await ArrangeAsync(Routes(("POST /notes", new HttpReply(200, Location: null, "made"))));

        var reply = await api.Transport.SendAsync(Call(HttpVerb.Post, api.Origin + "/notes", """{"title":"hi"}"""), CancellationToken.None);

        Expect.That(reply.IsSuccess && reply.Value.Status == 200 && api.Received().Any(static r => r.Contains("""{"title":"hi"}""", StringComparison.Ordinal)), "the body reached the server");
    }

    /// <summary>Headers are sent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Headers_reach_the_server()
    {
        var api = await ArrangeAsync(Routes(("GET /notes", new HttpReply(200, Location: null, "ok"))));
        var call = new HttpCall(HttpVerb.Get, api.Origin + "/notes", new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Probe"] = "yes" }, Body: null);

        _ = await api.Transport.SendAsync(call, CancellationToken.None);

        Expect.That(api.Received().Any(static r => r.Contains("X-Probe=yes", StringComparison.Ordinal)), "the header reached the server");
    }

    /// <summary>A host nothing listens on is AW-EVIDENCE-023.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unreachable_host_is_a_request_failure()
    {
        var api = await ArrangeUnreachableAsync();

        var reply = await api.Transport.SendAsync(Call(HttpVerb.Get, api.Origin + "/notes"), CancellationToken.None);

        Expect.That(reply.IsError && reply.Error.Code == EvidenceErrors.RequestFailed.Code, "no answer is AW-EVIDENCE-023");
    }

    private static HttpCall Call(HttpVerb method, string url, string? body = null) =>
        new(method, url, new Dictionary<string, string>(StringComparer.Ordinal), body);

    private static Dictionary<string, HttpReply> Routes(params (string Key, HttpReply Reply)[] routes) =>
        routes.ToDictionary(static r => r.Key, static r => r.Reply, StringComparer.Ordinal);
}
