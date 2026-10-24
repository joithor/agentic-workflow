using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;
using Aw.Context.Tests.Infrastructure;
using Aw.Context.Tests.Onyx;
using Aw.Kernel;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class BriefSourceTests
{
    private static BriefScope Scope(TempHome home, bool onyx = true) => new(
        new ContextSettings
        {
            LinearTeams = ["SPL"],
            Onyx = onyx ? OnyxSettings.Create("http://127.0.0.1:1/", OnyxSourceHarness.Pointer, FakeOnyxServer.PinnedVersion).Value : null,
        },
        [new ContextRepo(ContextRepoName.From("aw-split-panes"), "/nonexistent")], home.Scrubbers);

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_marks_brief_leaky_when_updated_after_cut()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));

        var issues = (await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken)).Value.Issues;

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105", "SPL-106"], issues.Select(i => i.Key), StringComparer.Ordinal);
        var first = issues[0];
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), first.CreatedAt);
        Assert.Equal("onyx/0e5a1b00-0000-4000-8000-000000000101", first.RecordRef);
        Assert.All(issues, i => Assert.True(i.UpdatedAt > i.CreatedAt));   // each brief was edited after its cut (§9.1): the corpus labels these leaky
        Assert.Equal("[SPL-101] Command registry for pane commands", first.Title);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_brief_is_the_cleaned_record_text()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = static json => json.Replace("named command in one registry", "named command in one registry " + Planted.Secret, StringComparison.Ordinal) });
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));
        var issue = (await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken)).Value.Issues[0];

        var brief = (await source.Brief(Scope(home), issue, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("SPL-101", brief!.Key);
        Assert.Contains("named command in one registry", brief.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Planted.Secret, brief.Text, StringComparison.Ordinal);   // cleaned at the ingest boundary
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_unknown_key_is_a_dropped_sample_not_an_error()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));
        var gone = new BriefIssue("SPL-999", "onyx/0e5a1b00-0000-4000-8000-000000009999", "v", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        var brief = await source.Brief(Scope(home), gone, TestContext.Current.CancellationToken);

        Assert.True(brief.IsSuccess);
        Assert.Null(brief.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_without_an_onyx_source_is_081()
    {
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog());

        var issues = await source.Issues(Scope(home, onyx: false), since: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.NoBriefSource.Code, issues.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_with_the_source_down_is_011()
    {
        using var harness = new OnyxSourceHarness(new Uri("http://127.0.0.1:1/"), "on_x", TimeSpan.FromSeconds(2));
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));

        var issues = await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, issues.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_listing_with_a_record_fetch_that_errors_fails_with_its_code_instead_of_shrinking_the_corpus()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(new FetchFailing(harness.Source)));

        var issues = await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, issues.Error.Code);
        Assert.Contains(ContextErrors.SourceUnavailable.Code, issues.Error.Message, StringComparison.Ordinal);   // the fetch's own code is named
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_names_the_team_whose_listing_hit_the_search_cap_instead_of_shrinking_in_silence()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(new FullPage(harness.Source, copies: 42)));   // 252 hits: the 250 the search asks for are used up

        var listing = (await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["SPL"], listing.CappedTeams);
        Assert.Equal(6, listing.Issues.Count);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_a_listing_under_the_cap_is_not_capped()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));

        var listing = (await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken)).Value;

        Assert.Empty(listing.CappedTeams);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_applies_the_window_start_in_the_search_before_the_cap()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));
        var all = (await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken)).Value.Issues;
        var since = all[3].CreatedAt;

        var windowed = (await source.Issues(Scope(home), since, TestContext.Current.CancellationToken)).Value.Issues;

        Assert.True(windowed.Count < all.Count);
        Assert.Equal(all.Where(i => i.UpdatedAt >= since).Select(i => i.Key), windowed.Select(i => i.Key), StringComparer.Ordinal);   // the source dropped what was updated before the window
        Assert.Contains(windowed, i => string.Equals(i.Key, all[3].Key, StringComparison.Ordinal));   // an issue created in the window was updated in it
    }

    [Fact, Trait("Lane", "unit")]
    public void OnyxSearchFilters_send_time_cutoff_only_when_a_window_start_is_named()
    {
        var empty = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(new OnyxAdminSearchRequest("q", new OnyxSearchFilters()), OnyxWire.Options));
        var windowed = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(new OnyxAdminSearchRequest("q", new OnyxSearchFilters(new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero))), OnyxWire.Options));

        Assert.DoesNotContain("time_cutoff", empty, StringComparison.Ordinal);
        Assert.Contains("\"time_cutoff\":\"2026-07-02T00:00:00", windowed, StringComparison.Ordinal);
    }

    private sealed class FullPage(IContextSource inner, int copies) : IContextSource
    {
        public SourceDescriptor Describe() => inner.Describe();

        public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
        {
            var hits = await inner.Search(query, cancellationToken).ConfigureAwait(false);
            return hits.IsError ? hits.Error : Results.Success<IReadOnlyList<SourceHit>>([.. Enumerable.Repeat(hits.Value, copies).SelectMany(static h => h)]);
        }

        public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => inner.Fetch(reference, cancellationToken);

        public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => inner.Versions(refs, cancellationToken);

        public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }

    private sealed class FetchFailing(IContextSource inner) : IContextSource
    {
        public SourceDescriptor Describe() => inner.Describe();

        public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => inner.Search(query, cancellationToken);

        public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<SourceRecord>>(ContextErrors.SourceUnavailable.With("onyx answered 503"));

        public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => inner.Versions(refs, cancellationToken);

        public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }
}
