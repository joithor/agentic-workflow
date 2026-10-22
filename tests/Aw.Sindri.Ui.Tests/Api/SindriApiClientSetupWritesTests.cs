using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's setup writes (setup-once spec §5, §7.2): the route, the verb, the body, who the app says it is, and that a password never reaches a log.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientSetupWritesTests
{
    private const string Sentinel = "hunter2-sentinel-9f3a";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddRepo_posts_the_path_to_the_repos_route_as_the_human_from_the_ui_and_never_as_a_terminal()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.SpecShaped("setup-repos", "started"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.AddRepo(_api, new AddRepoRequest("/work/demo"), Token);

        Assert.True(started.IsSuccess);
        Assert.Equal((HttpMethod.Post, "/sindri/setup/repos", "human", "ui", "tok-1", null), (canned.Method, canned.Path, canned.Caller, canned.Client, canned.Token, canned.Terminal));
        Assert.Contains("\"path\":\"/work/demo\"", canned.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_onyx_and_mcp_writes_post_to_their_routes()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.SpecShaped("onyx-setup", "started"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.SetupOnyx(_api, Token);
        var setup = canned.Path;
        _ = await client.RegisterMcp(_api, new RegisterMcpRequest(), Token);
        var mcp = canned.Path;
        canned.Respond(HttpStatusCode.OK, GoldenFiles.SpecShaped("onyx-defer", "deferred"));
        var deferred = await client.DeferOnyx(_api, new OnyxDeferRequest(Deferred: true), Token);

        Assert.Equal(("/sindri/onyx/setup", "/sindri/setup/mcp", "/sindri/onyx/defer", true), (setup, mcp, canned.Path, deferred.Value.Deferred));
        Assert.Contains("\"deferred\":true", canned.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectOnyx_sends_both_fields_once_to_the_account_route()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.SpecShaped("onyx-account", "started"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.ConnectOnyx(_api, new OnyxLogin("me@example.com", Sentinel), Token);

        Assert.True(started.IsSuccess);
        Assert.Equal((1, "/sindri/onyx/account"), (canned.Calls, canned.Path));
        Assert.Contains("\"adminEmail\":\"me@example.com\"", canned.Body, StringComparison.Ordinal);
        Assert.Contains("\"adminPassword\":\"" + Sentinel + "\"", canned.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_login_never_prints_its_password()
    {
        var login = new OnyxLogin("me@example.com", Sentinel);

        Assert.DoesNotContain(Sentinel, login.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("me@example.com", login.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"accepted\":42}")]   // an answer this build can't read
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.BadRequest, "{\"type\":\"about:blank\",\"title\":\"SND-ONYX-007\",\"status\":400,\"detail\":\"Onyx did not accept that email and password\",\"traceId\":\"t\"}")]
    [InlineData(HttpStatusCode.InternalServerError, "oops")]
    public async Task No_answer_to_a_login_puts_the_password_in_the_log_an_error_or_the_result(HttpStatusCode code, string body)
    {
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(new Canned(code, body)), new InMemoryHumanToken("tok-1"), TimeProvider.System, log);

        var result = await client.ConnectOnyx(_api, new OnyxLogin("me@example.com", Sentinel), Token);

        Assert.DoesNotContain(Sentinel, string.Join('\n', log.Entries.Select(e => e.Message + e.Exception)) + (result.IsError ? result.Error.Message : string.Empty), StringComparison.Ordinal);
        if (result.IsError)
        {
            Assert.DoesNotContain(Sentinel, result.Error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_unreachable_sindri_does_not_put_the_password_in_the_error()
    {
        var log = new RecordingLogger();
        var client = new SindriApiClient(new HttpClient(new Canned(HttpStatusCode.OK, "{}", fail: true)), new InMemoryHumanToken("tok-1"), TimeProvider.System, log);

        var result = await client.ConnectOnyx(_api, new OnyxLogin("me@example.com", Sentinel), Token);

        Assert.Equal("SND-UI-002", result.Error.Code);
        Assert.DoesNotContain(Sentinel, result.Error.Message + string.Join('\n', log.Entries.Select(e => e.Message + e.Exception)), StringComparison.Ordinal);
    }

    private sealed class Canned(HttpStatusCode code, string body, bool fail = false) : HttpMessageHandler
    {
        private HttpStatusCode _code = code;
        private string _answer = body;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Caller { get; private set; }

        public string? Client { get; private set; }

        public string? Token { get; private set; }

        public string? Terminal { get; private set; }

        public string? Body { get; private set; }

        public int Calls { get; private set; }

        public void Respond(HttpStatusCode status, string text) => (_code, _answer) = (status, text);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Calls++;
            (Method, Path) = (request.Method, request.RequestUri!.AbsolutePath); // why: HttpClient always sets the URI
            (Caller, Client, Token, Terminal) = (Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), Header("X-Sindri-Terminal"));
            Body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;
            if (fail)
            {
                throw new HttpRequestException("Connection refused (127.0.0.1:7410) with " + Body);
            }

            return new HttpResponseMessage(_code) { Content = new StringContent(_answer, Encoding.UTF8, "application/json") };
        }
    }
}
