using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Http;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>Replies the contract's server cannot produce: a charset nobody knows, and a server that never answers.</summary>
[Lane(TestLane.Integration)]
public sealed class HttpApiTransportTests
{
    private static HttpCall Get(string origin) => new(HttpVerb.Get, origin + "/notes", new Dictionary<string, string>(StringComparer.Ordinal), Body: null);

    [Fact]
    public async Task A_charset_nobody_knows_is_read_as_UTF8_not_thrown()
    {
        using var server = RawHttpServer.Answering("Content-Type: text/plain; charset=utf8mb4\r\n", "caf\u00e9 ok");
        using var transport = new HttpApiTransport(TimeSpan.FromSeconds(10));

        var reply = await transport.SendAsync(Get(server.Origin), TestContext.Current.CancellationToken);

        Assert.Equal("caf\u00e9 ok", reply.Value.Body);
    }

    [Fact]
    public async Task A_server_that_never_answers_is_a_request_failure_that_says_it_timed_out()
    {
        using var server = RawHttpServer.Silent();
        using var transport = new HttpApiTransport(TimeSpan.FromMilliseconds(300));

        var reply = await transport.SendAsync(Get(server.Origin), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RequestFailed.Code, reply.Error.Code);
        Assert.Contains("timed out", reply.Error.Message, StringComparison.Ordinal);
    }
}
