using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fakes;
using Aw.Context.Tests.Infrastructure;
using Aw.Context.Tests.Onyx;

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

        var issues = (await source.Issues(Scope(home), TestContext.Current.CancellationToken)).Value;

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
        var issue = (await source.Issues(Scope(home), TestContext.Current.CancellationToken)).Value[0];

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

        var issues = await source.Issues(Scope(home, onyx: false), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.NoBriefSource.Code, issues.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_with_the_source_down_is_011()
    {
        using var harness = new OnyxSourceHarness(new Uri("http://127.0.0.1:1/"), "on_x", TimeSpan.FromSeconds(2));
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(harness.Source));

        var issues = await source.Issues(Scope(home), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, issues.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task OnyxBriefSource_listing_with_a_record_fetch_that_errors_fails_with_its_code_instead_of_shrinking_the_corpus()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
        using var home = new TempHome();
        var source = new OnyxBriefSource(new FixedSourceCatalog(new FetchFailing(harness.Source)));

        var issues = await source.Issues(Scope(home), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, issues.Error.Code);
        Assert.Contains(ContextErrors.SourceUnavailable.Code, issues.Error.Message, StringComparison.Ordinal);   // the fetch's own code is named
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
