using System.Net;
using System.Text;
using System.Web;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's session reads (collector spec CA-1, CA-2): the routes, the escaping and what a 503 does to the shell.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientSessionsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task Session_escapes_provider_and_id_segments()
    {
        var canned = new Canned(HttpStatusCode.NotFound, GoldenFiles.Api("sessions-id", "not-found"));

        _ = await Client(canned).Session(_api, "claude/code", "a b?c#d", Token);

        var seen = Assert.Single(canned.Seen);
        Assert.Equal("/sindri/sessions/claude%2Fcode/a%20b%3Fc%23d", seen.AbsolutePath);
        Assert.Equal(string.Empty, seen.Query);
    }

    [Fact]
    public async Task Sessions_sends_each_filter_as_an_escaped_query_parameter_and_none_by_default()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("sessions", "empty"));
        var client = Client(canned);

        _ = await client.Sessions(_api, new SessionQuery(), Token);
        _ = await client.Sessions(_api, new SessionQuery(Provider: "claude-code", State: "asking", Repo: "a&b", InWorkflow: false), Token);

        Assert.Equal(string.Empty, canned.Seen[0].Query);
        var query = HttpUtility.ParseQueryString(canned.Seen[1].Query);
        Assert.Equal(["claude-code", "asking", "a&b", "false"], [query["provider"], query["state"], query["repo"], query["inWorkflow"]]);
        Assert.All(canned.Seen, u => Assert.Equal("/sindri/sessions", u.AbsolutePath));
    }

    [Fact]
    public async Task Both_reads_make_a_human_ui_call_with_the_token()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("sessions", "empty"));

        _ = await Client(canned).Sessions(_api, new SessionQuery(), Token);

        Assert.Equal(("human", "ui", "tok-1", null), canned.Headers[0]);
    }

    // Collector spec: SND-SESSION-002 is a page state. Only the follow's status and ping reads decide Not responding.
    [Fact]
    public async Task A_503_from_sessions_does_not_change_the_shell_state()
    {
        var server = new FakeSindriApi { SessionAnswer = RouteAnswer.Problem("sessions", "disabled") };
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var api = new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);
        var shell = new ShellViewModel(records, api, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);

        var read = await api.Sessions(_api, new SessionQuery(), Token);

        Assert.Equal("SND-SESSION-002", read.Error.Code);
        Assert.Equal((ShellState.Normal, false), (shell.State, shell.Health.Pill.IsNotResponding));
        Assert.True((await api.Status(_api, Token)).IsSuccess);
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        public List<(string? Caller, string? Client, string? Token, string? Terminal)> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            Headers.Add((Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), Header("X-Sindri-Terminal")));
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
