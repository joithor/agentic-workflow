using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// Doctor's 'context:onyx' version check against the fake Onyx v4.9.0 on loopback (spec §8.8). The deadline is
/// generous so load can't turn an answer into SND-CONTEXT-010; OnyxVersionRequestTests pins the expiry itself. Each
/// assertion on the state carries the health's code and detail, so a timeout reads differently from a wrong version.
/// </summary>
public sealed class OnyxVersionProbeTests
{
    private static readonly TimeSpan _deadline = OnyxSourceHarness.Patient;

    [Theory, Trait("Lane", "integration")]
    [InlineData("v4.9.0")]
    [InlineData("4.9.0")]
    public async Task Onyx_at_the_pinned_version_is_ok(string pinned)
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var http = Http();

        var health = await new OnyxVersionProbe(http).Check(Settings(onyx.Url, pinned), _deadline, TestContext.Current.CancellationToken);

        Assert.True((health.State, health.Code) == (SourceState.Ok, null), Shown(health));
        Assert.Contains("v4.9.0", health.Detail, StringComparison.Ordinal);
        var seen = Assert.Single(onyx.Requests);
        Assert.Equal(("GET", "/api/version", 200), (seen.Method, seen.Path, seen.Status));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Onyx_at_another_version_is_unavailable_with_055_naming_both()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxVersion = "v4.8.1" });
        using var http = Http();

        var health = await new OnyxVersionProbe(http).Check(Settings(onyx.Url, "v4.9.0"), _deadline, TestContext.Current.CancellationToken);

        Assert.True((health.State, health.Code) == (SourceState.Unavailable, ContextErrors.OnyxNotPinnedOrLoopback.Code), Shown(health));
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

        using var http = Http();

        var health = await new OnyxVersionProbe(http).Check(Settings(url, "v4.9.0"), _deadline, TestContext.Current.CancellationToken);

        Assert.True((health.State, health.Code) == (SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), Shown(health));
        Assert.Contains(url.Authority, health.Detail, StringComparison.Ordinal);
    }

    /// <summary>A client configured like production's OnyxHttp.Client: no proxy, no redirects.</summary>
    private static HttpClient Http() => new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });

    private static string Shown(SourceHealth health) => $"{health.State} {health.Code}: {health.Detail}";

    private static OnyxSettings Settings(Uri url, string version) =>
        OnyxSettings.Create(url.ToString(), "env:ONYX_FIXTURE_KEY", version).Value;
}
