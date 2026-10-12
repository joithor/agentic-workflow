using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Onyx;

/// <summary>Doctor's 'context:onyx' version check against the fake Onyx v4.9.0 on loopback (spec §8.8).</summary>
public sealed class OnyxVersionProbeTests
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(5);

    [Theory, Trait("Lane", "integration")]
    [InlineData("v4.9.0")]
    [InlineData("4.9.0")]
    public async Task Onyx_at_the_pinned_version_is_ok(string pinned)
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var http = new HttpClient();

        var health = await new OnyxVersionProbe(http).Check(Settings(onyx.Url, pinned), _deadline, TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Ok, health.State);
        Assert.Null(health.Code);
        Assert.Contains("v4.9.0", health.Detail, StringComparison.Ordinal);
        var seen = Assert.Single(onyx.Requests);
        Assert.Equal(("GET", "/api/version", 200), (seen.Method, seen.Path, seen.Status));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_at_another_version_is_unavailable_with_055_naming_both()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxVersion = "v4.8.1" });
        using var http = new HttpClient();

        var health = await new OnyxVersionProbe(http).Check(Settings(onyx.Url, "v4.9.0"), _deadline, TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Unavailable, health.State);
        Assert.Equal(ContextErrors.OnyxNotPinnedOrLoopback.Code, health.Code);
        Assert.Contains("'v4.8.1'", health.Detail, StringComparison.Ordinal);
        Assert.Contains("'v4.9.0'", health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Nothing_listening_is_unavailable_with_010()
    {
        Uri url;
        await using (var onyx = await FakeOnyxServer.Start())
        {
            url = onyx.Url;
        }

        using var http = new HttpClient();

        var health = await new OnyxVersionProbe(http).Check(Settings(url, "v4.9.0"), _deadline, TestContext.Current.CancellationToken);

        Assert.Equal(SourceState.Unavailable, health.State);
        Assert.Equal(ContextErrors.SourceUnavailable.Code, health.Code);
        Assert.Contains(url.Authority, health.Detail, StringComparison.Ordinal);
    }

    private static OnyxSettings Settings(Uri url, string version) =>
        OnyxSettings.Create(url.ToString(), "env:ONYX_FIXTURE_KEY", version).Value;
}
