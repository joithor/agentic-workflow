using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's setup read (setup-once spec §7): the route, the verb and who the app says it is.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientSetupTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Setup_is_a_get_of_the_setup_route_made_as_the_human_from_the_ui_with_the_token()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("setup", "first-run"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var view = await client.Setup(_api, Token);

        Assert.True(view.IsSuccess);
        Assert.Equal((HttpMethod.Get, "/sindri/setup", "human", "ui", "tok-1"), (canned.Method, canned.Path, canned.Caller, canned.Client, canned.Token));
    }

    [Fact]
    public async Task Setup_reads_a_step_without_items_as_having_none()
    {
        const string body = """{"steps":[{"id":"repo","title":"Add your repo","state":"needs-you","why":"Pick a folder.","detail":null,"error":null,"action":"AddRepo","progress":null}]}""";
        var client = new SindriApiClient(new HttpClient(new Canned(HttpStatusCode.OK, body)), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var view = await client.Setup(_api, Token);

        Assert.True(view.IsSuccess);
        Assert.Null(view.Value.Steps[0].Items);
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Caller { get; private set; }

        public string? Client { get; private set; }

        public string? Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            (Method, Path) = (request.Method, request.RequestUri!.AbsolutePath); // why: HttpClient always sets the URI
            (Caller, Client, Token) = (Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"));
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
