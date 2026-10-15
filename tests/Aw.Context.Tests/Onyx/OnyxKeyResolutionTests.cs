using Aw.Context.Core;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// The onyx source resolves its key pointer once per source, that is once per query (spec §4 "Secrets"): a keychain:
/// or op: pointer starts a process per resolution, so a search and a many-chunk fetch must not resolve it per request.
/// A pointer that didn't resolve is tried again by the next query's source. The resolution runs under the call's
/// deadline and the caller's cancellation, so a locked keychain or an op sign-in degrades the query on time.
/// </summary>
[Trait("Lane", "integration")]
public sealed class OnyxKeyResolutionTests
{
    [Fact]
    public async Task A_search_and_a_multi_chunk_fetch_resolve_the_pointer_once()
    {
        await using var onyx = await FakeOnyxServer.Start();
        var resolutions = 0;
        using var harness = new OnyxSourceHarness(onyx.Url, name =>
        {
            _ = Interlocked.Increment(ref resolutions);
            return name == OnyxSourceHarness.Pointer ? onyx.Token : null;
        });

        var hit = (await harness.Source.Search(OnyxSourceHarness.Query("minimum pane width"), TestContext.Current.CancellationToken)).Value[0];
        var record = await harness.Source.Fetch(hit.Ref, TestContext.Current.CancellationToken);

        Assert.True(record.IsSuccess);
        Assert.True(onyx.Requests.Where(r => r.Path.StartsWith("/api/document/chunk-info", StringComparison.Ordinal)).Skip(1).Any());
        Assert.Equal(1, resolutions);
    }

    [Fact]
    public async Task A_pointer_that_did_not_resolve_is_resolved_again_by_the_next_source()
    {
        await using var onyx = await FakeOnyxServer.Start();
        string? key = null;
        using var harness = new OnyxSourceHarness(onyx.Url, name => name == OnyxSourceHarness.Pointer ? key : null);

        var first = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        key = onyx.Token;
        var again = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        var next = await harness.Sources.Create(harness.Settings, OnyxSourceHarness.Teams).Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, first.Error.Code);
        Assert.Equal(ContextErrors.SourceNotConfigured.Code, again.Error.Code);   // the same source keeps its answer
        Assert.NotEmpty(next.Value);
    }

    [Fact]
    public async Task A_resolution_slower_than_the_deadline_is_unavailable_with_010_within_the_deadline()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var stuck = new ManualResetEventSlim();
        using var harness = new OnyxSourceHarness(onyx.Url, _ => stuck.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken) ? null : onyx.Token);
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var search = await harness.Source.Search(OnyxSourceHarness.Query("pane") with { Deadline = TimeSpan.FromMilliseconds(200) }, TestContext.Current.CancellationToken);

            Assert.Equal(ContextErrors.SourceUnavailable.Code, search.Error.Code);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(3), $"took {started.Elapsed}");
            Assert.Empty(onyx.Requests);
        }
        finally
        {
            stuck.Set();
        }
    }

    [Fact]
    public async Task Cancelling_the_caller_stops_a_resolution_that_is_still_running()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var stuck = new ManualResetEventSlim();
        using var harness = new OnyxSourceHarness(onyx.Url, _ => stuck.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken) ? null : onyx.Token);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(TimeSpan.FromMilliseconds(200));
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Source.Search(OnyxSourceHarness.Query("pane"), caller.Token));

            Assert.True(started.Elapsed < TimeSpan.FromSeconds(3), $"took {started.Elapsed}");
        }
        finally
        {
            stuck.Set();
        }
    }

    [Fact]
    public async Task A_resolution_that_throws_is_002_not_an_exception()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, _ => throw new IOException("fixture: the pointer's file can't be read"));

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceNotConfigured.Code, search.Error.Code);
        Assert.DoesNotContain("fixture: the pointer's file", search.Error.Message, StringComparison.Ordinal);
        Assert.Empty(onyx.Requests);
    }

    [Fact]
    public async Task A_search_whose_deadline_is_already_spent_is_010_not_an_exception()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, name => name == OnyxSourceHarness.Pointer ? onyx.Token : null);

        var search = await harness.Source.Search(OnyxSourceHarness.Query("pane") with { Deadline = TimeSpan.FromMilliseconds(-250) }, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, search.Error.Code);
    }
}
