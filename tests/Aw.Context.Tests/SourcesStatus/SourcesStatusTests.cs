using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.QueryContext;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SourcesStatus;

public sealed class SourcesStatusTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_never_refreshed_is_degraded_with_the_fix()
    {
        var view = await Engines.Over(new FixedSourceCatalog()).Sources(Engines.Human(), CancellationToken.None);

        var row = view.Value.Sources[0];
        Assert.Equal(("repo-docs", "split-panes", SourceState.Degraded), (row.Source.Value, row.Repo?.Value, row.State));
        Assert.Contains("context-docs-refresh", row.Detail, StringComparison.Ordinal);
        Assert.False(view.Value.SessionsEnabled);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task After_a_refresh_each_repo_reports_its_counts_and_a_failed_one_its_code()
    {
        using var repo = FixtureRepo.Build();
        var index = new FakeDocIndex(new FakeTimeProvider());
        var engine = Engines.Over(new FixedSourceCatalog(), index);
        var scope = Engines.Human() with { Repos = [new ContextRepo(ContextRepoName.From("split-panes"), repo.Path)] };

        var report = await engine.Refresh(scope, CancellationToken.None);
        var view = await engine.Sources(scope, CancellationToken.None);
        index.RefreshCode = "SND-CONTEXT-004";
        _ = await engine.Refresh(scope, CancellationToken.None);
        var failed = await engine.Sources(scope, CancellationToken.None);

        Assert.Equal(2, report.Value.Repos.Single().Added);
        Assert.Equal((SourceState.Ok, 2), (view.Value.Sources.Single().State, view.Value.Sources.Single().Records));
        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-004"), (failed.Value.Sources.Single().State, failed.Value.Sources.Single().Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Live_sources_report_their_probe_and_sessions_are_gated()
    {
        var onyx = new FakeContextSource(new SourceDescriptor(SourceId.From("onyx"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), [])
        {
            Failure = ContextErrors.SourceUnavailable.With("down"),
        };
        var engine = Engines.Over(new FixedSourceCatalog(onyx));

        var view = await engine.Sources(Engines.Human(), CancellationToken.None);
        var session = await engine.Sources(Engines.Session("split-panes"), CancellationToken.None);

        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-010"), (view.Value.Sources[^1].State, view.Value.Sources[^1].Code));
        Assert.Equal(ContextErrors.SessionsDisabled.Code, session.Error.Code);
    }
}
