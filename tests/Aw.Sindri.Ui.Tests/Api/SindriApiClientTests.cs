using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

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

        var status = await new SindriApiClient(handler).Status(_api, Token);

        Assert.Equal("SND-UI-003", status.Error.Code);
        Assert.Contains("500", status.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_isnt_the_record_is_an_answer_the_app_cant_read()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.OK, "<html>not sindri</html>") };

        var ping = await new SindriApiClient(handler).Ping(_api, Token);

        Assert.Equal("SND-UI-003", ping.Error.Code);
    }

    [Fact]
    public async Task An_event_stream_that_isnt_ok_ends_at_once()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { Override = (HttpStatusCode.NotFound, "{}") };

        var events = await new SindriApiClient(handler).Events(_api, 0, Token).ToListAsync(Token);

        Assert.Empty(events);
    }
}
