using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Features;
using Aw.Context.Features.ApplyReviewFile;
using Aw.Context.Features.BuildMap;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ExportReview;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.GetProposals;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PromoteMap;
using Aw.Context.Features.ProposeFromScope;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.ReviewMap;
using Aw.Context.Features.ReviewQueue;
using Aw.Context.Features.ShowMapStatus;
using Aw.Context.Features.ShowNode;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.EvalProbes;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The facade over fakes, and the scopes the tests ask in.</summary>
internal static class Engines
{
    internal static readonly ContextRepo[] Repos = [new(ContextRepoName.From("split-panes"), "/repos/split-panes"), new(ContextRepoName.From("other"), "/repos/other")];

    internal static IContextEngine Over(FixedSourceCatalog catalog, FakeDocIndex? index = null, FakeTimeProvider? time = null)
    {
        var clock = time ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var scrubbers = new FakeScrubberFactory();
        var docs = index ?? new FakeDocIndex(clock);
        var store = new FakeGraphStore();
        var graphs = new MapReader(store);
        return new ContextEngine(new QueryContextService(catalog, scrubbers, new SequentialIdGenerator(), clock, graphs),
            new SourcesStatusService(docs, catalog, scrubbers, clock), new RefreshDocsService(docs, scrubbers), scrubbers, Map(clock, store),
            new ContextGraph(new ShowNodeService(graphs, scrubbers), new ListNeighborsService(graphs, scrubbers), new FindPathService(graphs, scrubbers),
                new GetLensesService(), new GetFileMapService()),
            Probes(clock));
    }

    /// <summary>The probe runner over no probe file (the query tests never run probes).</summary>
    private static EvalProbesService Probes(TimeProvider clock) => new(new NoProbes(), new NoProbes(), clock);

    /// <summary>The map part over its fakes (S-3a); the query tests never touch it.</summary>
    private static ContextMap Map(TimeProvider clock, FakeGraphStore store)
    {
        var exports = new FakeExportStore(clock);
        var reader = new FakeMapFileReader();
        var admin = new MapAdmin(new PreviewMapService(reader), new ImportMapService(reader, store, clock), new ExportMapService(store, exports, clock),
            new ShowMapStatusService(store), new CheckMapService(store), new CheckRetentionService(store, exports, clock), new PurgeMapService(store, exports, clock));
        var review = new MapReview(new ReviewQueueService(), new ReviewMapService(), new ExportReviewService(), new ApplyReviewFileService(), new PromoteMapService());
        return new ContextMap(admin, review, new MapProposals(new BuildMapService(), new ProposeFromScopeService(), new GetProposalsService()));
    }

    internal static IContextEngine Over(params IContextSource[] sources) => Over(new FixedSourceCatalog(sources));

    /// <summary>
    /// Runs <paramref name="query"/> while advancing <paramref name="clock"/> a second at a time, so a deadline on the
    /// query's clock passes without real waiting. A query still running after that fails the test within 10 s of real
    /// time instead of hanging the lane.
    /// </summary>
    internal static async Task<T> Advancing<T>(FakeTimeProvider clock, Func<Task<T>> query)
    {
        var running = query();
        for (var i = 0; i < 30 && !running.IsCompleted; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        return await running.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken);
    }

    internal static ContextScope Human(string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    internal static ContextScope Session(string? repo, string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Session, repo is null ? null : ContextRepoName.From(repo), ContextClient.Http));

    private static JsonElement? Settings(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
