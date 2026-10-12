using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

[Lane(TestLane.Unit)]
public sealed class SindriApiClientTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_error_status_is_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.InternalServerError, """{"title":"SND-LEDGER-002"}""") };

        var status = await new SindriApiClient(new HttpClient(handler), TimeProvider.System).Status(_api, Token);

        Assert.Equal("SND-UI-003", status.Error.Code);
        Assert.Contains("500", status.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_isnt_the_record_is_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, "<html>not sindri</html>") };

        var ping = await new SindriApiClient(new HttpClient(handler), TimeProvider.System).Ping(_api, Token);

        Assert.Equal("SND-UI-003", ping.Error.Code);
    }

    // Prover run 7 (§4.1 Unreadable, §12.4, as I7 for SND-UI-002): a body this build can't read is a plain sentence naming
    // the route, never the parser's text (a .NET type name, a JSON path, a byte position). That text goes to the app's log.
    [Fact]
    public async Task A_body_this_build_cant_read_is_a_plain_sentence_and_the_parsers_text_goes_to_the_log()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, """{"pid":"not-a-number","version":7}""") };
        var log = new RecordingLogger();

        var status = await new SindriApiClient(new HttpClient(handler), TimeProvider.System, log).Status(_api, Token);

        Assert.Equal("SND-UI-003", status.Error.Code);
        Assert.Equal("/sindri/status answered with something this build can't read", status.Error.Message);
        Assert.All((string[])["System.", "Path:", "BytePosition", "$."], raw => Assert.DoesNotContain(raw, status.Error.Message, StringComparison.Ordinal));
        var entry = Assert.Single(log.Entries);
        Assert.Equal(("SND-UI-003", LogLevel.Warning), (entry.EventName, entry.Level));
        Assert.Contains("/sindri/status", entry.Message, StringComparison.Ordinal);
        Assert.Contains("BytePosition", entry.Message, StringComparison.Ordinal);
    }

    // I7: nothing listening is SND-UI-002 without the platform's exception text (it repeated the address in .NET's words).
    [Fact]
    public async Task Nothing_listening_is_unreachable_without_the_exception_text()
    {
        var server = new FakeSindriApi();
        server.GoDown();

        var ping = await new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), TimeProvider.System).Ping(_api, Token);

        Assert.Equal("SND-UI-002", ping.Error.Code);
        Assert.DoesNotContain("Connection refused", ping.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_event_stream_that_isnt_ok_ends_at_once()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.NotFound, "{}") };

        var events = await new SindriApiClient(new HttpClient(handler), TimeProvider.System).Events(_api, 0, Token).ToListAsync(Token);

        Assert.Empty(events);
    }

    // UI spec §9.1, U2: the app is a human with no terminal: X-Sindri-Caller human and X-Sindri-Client ui on every request,
    // never X-Sindri-Terminal and never Origin.
    [Fact]
    public async Task Every_request_says_human_from_the_ui_and_never_terminal_or_origin()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(1));
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), TimeProvider.System);

        _ = await client.Ping(_api, Token);
        _ = await client.Status(_api, Token);
        await foreach (var _ in client.Events(_api, 0, Token))
        {
            server.Drop();
        }

        Assert.Equal(["/sindri/ping", "/sindri/status", "/sindri/events"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
        Assert.All(handler.Requests, r => Assert.Equal(new SeenRequest(r.Path, "human", "ui", Terminal: null, Origin: null), r));
    }

    // SND-UI-002's 2 s rule runs on the app's clock, like every other timing rule: a call that hangs is unreachable
    // once the clock passes 2 s, with no real wait.
    [Fact]
    public async Task A_call_that_doesnt_answer_within_2_s_on_the_apps_clock_is_unreachable()
    {
        var clock = new FakeTimeProvider(A.Noon);
        using var http = new HttpClient(new SilentHandler());
        var ping = new SindriApiClient(http, clock).Ping(_api, Token);

        clock.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        Assert.False(ping.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        var result = await ping.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, Token);

        Assert.Equal("SND-UI-002", result.Error.Code);
        Assert.Contains("didn't answer within 2 s", result.Error.Message, StringComparison.Ordinal);
    }

    // A server that accepts the request and never answers.
    private sealed class SilentHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
