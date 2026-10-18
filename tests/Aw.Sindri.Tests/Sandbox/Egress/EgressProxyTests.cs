using System.Net;
using System.Text;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressProxyTests
{
    private static readonly IReadOnlyList<PlanGroup> _groups = [new("model", ["api.anthropic.com", "plain.example", "inside.example"])];

    private static readonly Dictionary<string, IPAddress[]> _dns = new(StringComparer.Ordinal)
    {
        ["api.anthropic.com"] = [IPAddress.Parse("160.79.104.10")],
        ["plain.example"] = [IPAddress.Parse("93.184.216.34")],
        ["inside.example"] = [IPAddress.Parse("10.0.0.7")],
    };

    private static (EgressProxy Proxy, FakeEgressDialer Dialer, EgressLog Log) Make(EgressCaps? caps = null)
    {
        var dialer = new FakeEgressDialer();
        var log = new EgressLog(Path.Combine("/nonexistent-egress-dir", "egress.log"));
        return (new EgressProxy(new EgressPolicy(_groups, new FakeEgressResolver(_dns)), dialer, log, caps ?? EgressCaps.Default), dialer, log);
    }

    private static async Task<string> ReadHead(Stream stream)
    {
        var text = new StringBuilder();
        var one = new byte[1];
        while (!text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && await stream.ReadAsync(one, TestContext.Current.CancellationToken) > 0)
        {
            _ = text.Append((char)one[0]);
        }

        return text.ToString();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proxy_relays_connect_to_an_allowed_host_and_counts_it()
    {
        var (proxy, dialer, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\nHost: api.anthropic.com:443\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.StartsWith("HTTP/1.1 200", await ReadHead(client), StringComparison.Ordinal);
        var upstream = await dialer.Upstream.Task;
        await client.WriteAsync("ping"u8.ToArray(), TestContext.Current.CancellationToken);
        var got = new byte[4];
        await upstream.ReadExactlyAsync(got, TestContext.Current.CancellationToken);
        await upstream.WriteAsync("pong"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.ReadExactlyAsync(got, TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        await handled;

        Assert.Equal("pong", Encoding.ASCII.GetString(got));
        Assert.Equal([(IPAddress.Parse("160.79.104.10"), 443)], dialer.Dialed);
        Assert.Equal(1, log.Count("api.anthropic.com", 443, EgressVerdict.Allowed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proxy_rewrites_an_absolute_uri_request_for_plain_http()
    {
        var (proxy, dialer, _) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("GET http://plain.example/ocsp?x=1 HTTP/1.1\r\nHost: plain.example\r\nProxy-Connection: keep-alive\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var upstream = await dialer.Upstream.Task;
        var head = await ReadHead(upstream);
        await client.DisposeAsync();
        await handled;

        Assert.StartsWith("GET /ocsp?x=1 HTTP/1.1\r\n", head, StringComparison.Ordinal);
        Assert.DoesNotContain("Proxy-Connection", head, StringComparison.Ordinal);
        Assert.Contains("Connection: close", head, StringComparison.Ordinal);
        Assert.Equal(80, dialer.Dialed.Single().Port);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Host_outside_the_group_gets_403_and_is_counted_denied()
    {
        var (proxy, dialer, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT evil.example:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var head = await ReadHead(client);
        await client.DisposeAsync();
        await handled;

        Assert.StartsWith("HTTP/1.1 403", head, StringComparison.Ordinal);
        Assert.Empty(dialer.Dialed);
        Assert.Equal(1, log.Count("evil.example", 443, EgressVerdict.Denied));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Loopback_request_through_the_proxy_is_refused_which_is_why_NO_PROXY_names_it()
    {
        var (proxy, dialer, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("POST http://127.0.0.1:7480/sindri/mcp HTTP/1.1\r\nHost: 127.0.0.1:7480\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var head = await ReadHead(client);
        await client.DisposeAsync();
        await handled;

        Assert.StartsWith("HTTP/1.1 403", head, StringComparison.Ordinal);
        Assert.Empty(dialer.Dialed);
        Assert.Equal(1, log.Count("127.0.0.1", 7480, EgressVerdict.Denied));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Caps_refuse_a_connection_over_the_limit_with_503()
    {
        var (proxy, dialer, log) = Make(new EgressCaps(1, 1024, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)));
        var (first, firstServer) = JoinedStream.Pair();
        var firstHandled = proxy.Handle(firstServer, TestContext.Current.CancellationToken);
        await first.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        _ = await ReadHead(first);

        var (second, secondServer) = JoinedStream.Pair();
        await proxy.Handle(secondServer, TestContext.Current.CancellationToken);
        var head = await ReadHead(second);
        await first.DisposeAsync();
        await firstHandled;

        Assert.StartsWith("HTTP/1.1 503", head, StringComparison.Ordinal);
        _ = Assert.Single(dialer.Dialed);
        Assert.Equal(1, log.Count("(cap)", 0, EgressVerdict.Refused));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Bytes_cap_ends_the_relay()
    {
        var (proxy, dialer, _) = Make(new EgressCaps(4, 8, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)));
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);
        await client.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        _ = await ReadHead(client);
        _ = await dialer.Upstream.Task;

        await client.WriteAsync(new byte[64], TestContext.Current.CancellationToken);

        await handled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Connection_lifetime_ends_a_connection()
    {
        var (proxy, dialer, _) = Make(new EgressCaps(4, 1024, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(30)));
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);
        await client.WriteAsync("CONNECT api.anthropic.com:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        _ = await ReadHead(client);
        _ = await dialer.Upstream.Task;

        await handled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Handle_does_not_touch_the_disk()
    {
        var (proxy, _, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT evil.example:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        _ = await ReadHead(client);
        await client.DisposeAsync();

        await handled;
        Assert.Equal(1, log.Count("evil.example", 443, EgressVerdict.Denied));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_that_resolves_to_a_private_address_is_403_and_counted_refused()
    {
        var (proxy, dialer, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT inside.example:443 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var head = await ReadHead(client);
        await client.DisposeAsync();
        await handled;

        Assert.StartsWith("HTTP/1.1 403", head, StringComparison.Ordinal);
        Assert.Empty(dialer.Dialed);
        Assert.Equal(1, log.Count("inside.example", 443, EgressVerdict.Refused));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_port_outside_80_and_443_is_403_and_counted_denied()
    {
        var (proxy, dialer, log) = Make();
        var (client, server) = JoinedStream.Pair();
        var handled = proxy.Handle(server, TestContext.Current.CancellationToken);

        await client.WriteAsync("CONNECT api.anthropic.com:22 HTTP/1.1\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var head = await ReadHead(client);
        await client.DisposeAsync();
        await handled;

        Assert.StartsWith("HTTP/1.1 403", head, StringComparison.Ordinal);
        Assert.Empty(dialer.Dialed);
        Assert.Equal(1, log.Count("api.anthropic.com", 22, EgressVerdict.Denied));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_client_that_never_sends_its_head_is_closed_after_the_head_timeout()
    {
        var (proxy, _, _) = Make(new EgressCaps(4, 1024, TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(200)));
        var (client, server) = JoinedStream.Pair();

        await proxy.Handle(server, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await client.DisposeAsync();
    }
}
