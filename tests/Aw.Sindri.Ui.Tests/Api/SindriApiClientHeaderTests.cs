using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The headers the app sends (UI spec §9.1, API §2.4, §6): the human token, and X-Sindri-After once after a command.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientHeaderTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // API P1: serve mints <home>/auth/human.token; a human call without it is refused, so the app sends it on every call.
    [Fact]
    public async Task Every_call_carries_this_starts_human_token_and_the_api_lets_it_in()
    {
        var server = new FakeSindriApi { HumanToken = "tok-1" };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System, new InMemoryHumanTokens { Text = "tok-1\n" });

        Assert.True((await client.Ping(_api, Token)).IsSuccess);
        Assert.True((await client.Commands(_api, Token)).IsSuccess);
        Assert.True((await client.RebuildViews(_api, Token)).IsSuccess);

        Assert.All(handler.Requests, r => Assert.Equal("tok-1", r.Token));
    }

    // A restarted control plane mints a new token: the app reads the source before each call, so it never sends a stale one.
    [Fact]
    public async Task A_new_token_is_used_from_the_next_call()
    {
        var tokens = new InMemoryHumanTokens { Text = "tok-1" };
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System, tokens);

        _ = await client.Ping(_api, Token);
        tokens.Text = "tok-2";
        _ = await client.Ping(_api, Token);

        Assert.Equal(["tok-1", "tok-2"], handler.Requests.Select(r => r.Token), StringComparer.Ordinal);
    }

    // No token file (or one the platform can't read): the app sends none, and shows the API's refusal as served.
    [Fact]
    public async Task Without_a_token_the_refusal_is_the_apis_SND_AUTH_001_and_its_fix()
    {
        var tokens = new InMemoryHumanTokens { Text = null };
        var handler = new FakeSindriHandler(new FakeSindriApi { HumanToken = "tok-1" });
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System, tokens);

        var status = await client.Status(_api, Token);
        tokens.FailNextRead(new IOException("denied"));
        var again = await client.Status(_api, Token);

        Assert.Equal("SND-AUTH-001", status.Error.Code);
        // The detail is the API's, as served (keyed off the golden, never a copy of its sentence, which P2 rewords).
        var served = System.Text.Json.JsonDocument.Parse(GoldenFiles.Api("status", "human-no-token")).RootElement.GetProperty("detail").GetString();
        Assert.Equal(served, status.Error.Message);
        Assert.Equal("SND-AUTH-001", again.Error.Code);
        Assert.All(handler.Requests, r => Assert.Null(r.Token));
    }

    // API §2.4: after an accepted command the next query carries its sequence once; the stream and commands never do.
    [Fact]
    public async Task The_query_after_an_accepted_command_waits_for_its_sequence_once()
    {
        var server = new FakeSindriApi { StreamsEndAtOnce = true };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System, new InMemoryHumanTokens { Text = "tok-1" });

        _ = await client.Status(_api, Token);
        _ = await client.RebuildViews(_api, Token);
        _ = await client.Events(_api, 0, Token).ToListAsync(Token);
        _ = await client.Services(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Equal(
            [("GET", "/sindri/status", null), ("POST", "/sindri/views/rebuild", null), ("GET", "/sindri/events", null), ("GET", "/sindri/services", "43"), ("GET", "/sindri/status", null)],
            handler.Requests.Select(r => (r.Method, r.Path, r.After)));
    }

    // A command is a human's from the UI like every call: never X-Sindri-Terminal (protected stays at the CLI), never Origin.
    [Fact]
    public async Task A_command_says_human_from_the_ui_and_never_terminal_or_origin()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System, new InMemoryHumanTokens { Text = "tok-1" });

        _ = await client.RebuildViews(_api, Token);

        Assert.Equal(new SeenRequest("/sindri/views/rebuild", "human", "ui", Terminal: null, Origin: null, Token: "tok-1", After: null, Method: "POST"), Assert.Single(handler.Requests));
    }
}
