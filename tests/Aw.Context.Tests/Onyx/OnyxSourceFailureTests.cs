using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// How the onyx source fails (spec §5.1, §8.6, §8.8): a refused key is SND-CONTEXT-050, an unresolved pointer 002, no
/// answer in time 010; it sends only its own five requests; and the key never reaches a log line or an error.
/// </summary>
[Trait("Lane", "integration")]
public sealed class OnyxSourceFailureTests
{
    private const string Spl105 = "0e5a1b00-0000-4000-8000-000000000105";

    private static SourceRef Spl105Ref => new(SourceId.From("onyx"), ExternalId.From(Spl105),
        SourceVersion.From("2026-08-12T15:00:00.0000000Z"), Permalink: null, RecordKey.From("linear:issue/" + Spl105));

    [Fact]
    public async Task A_key_onyx_refuses_is_050_for_search_and_fetch()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, "on_not-the-fixture-key");

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        var fetch = await harness.Source.Fetch(Spl105Ref, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.OnyxKeyRefused.Code, search.Error.Code);
        Assert.Equal(Kernel.ErrorKind.Forbidden, search.Error.Kind);
        Assert.Equal(ContextErrors.OnyxKeyRefused.Code, fetch.Error.Code);
    }

    [Fact]
    public async Task A_pointer_that_resolves_to_nothing_is_002_and_sends_nothing()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, token: null);

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, search.Error.Code);
        Assert.Contains("context.sources.onyx.token", search.Error.Message, StringComparison.Ordinal);
        Assert.Empty(onyx.Requests);
    }

    [Fact]
    public async Task Onyx_not_running_is_unavailable_with_010_for_every_call()
    {
        Uri url;
        await using (var onyx = await FakeOnyxServer.Start())
        {
            url = onyx.Url;
        }

        using var harness = new OnyxSourceHarness(url, FakeOnyxServer.DefaultToken);

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        var fetch = await harness.Source.Fetch(Spl105Ref, TestContext.Current.CancellationToken);
        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal((ContextErrors.SourceUnavailable.Code, Kernel.ErrorKind.Unavailable), (search.Error.Code, search.Error.Kind));
        Assert.Equal(ContextErrors.SourceUnavailable.Code, fetch.Error.Code);
        Assert.Equal((SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), (health.State, health.Code));
        Assert.Contains(url.Authority, search.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_the_deadline_is_unavailable_with_010()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Delay = TimeSpan.FromSeconds(5) });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane") with { Deadline = TimeSpan.FromMilliseconds(200) }, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, search.Error.Code);
        Assert.Contains("within", search.Error.Message, StringComparison.Ordinal);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Missing_the_fetch_and_probe_call_deadline_is_unavailable_with_010()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Delay = TimeSpan.FromSeconds(5) });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token, callDeadline: TimeSpan.FromMilliseconds(200));
        var started = System.Diagnostics.Stopwatch.StartNew();

        var fetch = await harness.Source.Fetch(Spl105Ref, TestContext.Current.CancellationToken);
        var health = await harness.Source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, fetch.Error.Code);
        Assert.Contains("within", fetch.Error.Message, StringComparison.Ordinal);
        Assert.Equal((SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), (health.State, health.Code));
        Assert.Contains("within", health.Detail, StringComparison.Ordinal);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(3), $"took {started.Elapsed}");
    }

    [Theory]
    [InlineData("DELETE", "/api/admin/search")]
    [InlineData("GET", "/api/admin/search")]
    [InlineData("POST", "/api/me")]
    [InlineData("GET", "/api/manage/users")]
    [InlineData("POST", "/api/admin/api-key")]
    [InlineData("GET", "/api/me/../admin/search")]
    [InlineData("GET", "/api/version/")]
    [InlineData("GET", "api/version")]
    public async Task A_request_off_the_allowlist_is_refused_before_it_is_sent(string method, string route)
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await OnyxRequests.Send(harness.Http, onyx.Url, new OnyxCall(new HttpMethod(method), route, "", Body: null), onyx.Token, TestContext.Current.CancellationToken));

        Assert.Contains("allowlist", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(onyx.Token, error.Message, StringComparison.Ordinal);
        Assert.Empty(onyx.Requests);
    }

    [Fact]
    public async Task The_allowlist_is_exactly_the_five_requests_the_adapter_makes()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);

        Assert.Equal(
            ["POST /api/admin/search", "GET /api/document/document-size-info", "GET /api/document/chunk-info", "GET /api/version", "GET /api/me"],
            OnyxRequests.Allowlist.Select(r => $"{r.Method} {r.Route}"), StringComparer.Ordinal);
        var reply = await OnyxRequests.Send(harness.Http, onyx.Url, new OnyxCall(HttpMethod.Get, "/api/me", "", Body: null), onyx.Token, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, reply.Status);
    }

    [Fact]
    public async Task The_key_never_reaches_a_log_line_an_error_or_a_health_detail()
    {
        const string planted = "on_planted-onyx-5e1f";
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Token = planted });
        using var good = new OnyxSourceHarness(onyx.Url, planted);
        using var wrong = new OnyxSourceHarness(onyx.Url, planted + "-wrong");
        Uri down;
        await using (var gone = await FakeOnyxServer.Start())
        {
            down = gone.Url;
        }

        using var offline = new OnyxSourceHarness(down, planted);

        var texts = new List<string>
        {
            (await good.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken)).Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            (await good.Source.Probe(TestContext.Current.CancellationToken)).Detail,
            (await wrong.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken)).Error.Message,
            (await wrong.Source.Fetch(Spl105Ref, TestContext.Current.CancellationToken)).Error.Message,
            (await wrong.Source.Probe(TestContext.Current.CancellationToken)).Detail,
            (await offline.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken)).Error.Message,
            (await offline.Source.Probe(TestContext.Current.CancellationToken)).Detail,
            good.Settings.ToString(),
        };

        Assert.NotEmpty(wrong.Log.Lines);
        Assert.NotEmpty(offline.Log.Lines);
        Assert.All(texts.Concat(good.Log.Lines).Concat(wrong.Log.Lines).Concat(offline.Log.Lines).Concat(onyx.Log),
            line => Assert.DoesNotContain("planted-onyx", line, StringComparison.Ordinal));
    }
}
