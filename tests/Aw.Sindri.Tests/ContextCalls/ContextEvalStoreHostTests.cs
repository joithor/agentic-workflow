using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The eval file store as the started host wires it (the host's own JSON options, no test context): the backtest report, the map-quality report and the
/// as-of map cache write and read back. A store that serialises through the host's context fails these with 'JsonTypeInfo metadata ... was not provided'.
/// </summary>
public sealed class ContextEvalStoreHostTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static BacktestReport Backtest() => new(1, _now, "all", 5, SmallSample: true, 1.0, new LeftOut(1, 0, 0, ["SPL-106"]), [], [], Verdicts.OwnerDecision, ["clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"]);

    private static MapQualityReport Quality() => new(1, _now, "v1", new Ratio(4, 5), new Ratio(3, 3), new Ratio(1, 2), new Ratio(2, 2), 0, new Ratio(1, 10), "2026-04-30..2026-07-29", ["node precision >= 0.80: met"]);

    private static ExtractedMap Map() =>
        new([new GraphNode(NodeKey.From("feature:a.one"), "one", Provenance.Deterministic), new GraphNode(NodeKey.From("code:src/a.ts"), "a.ts", Provenance.Deterministic)],
            [new GraphEdge(NodeKey.From("code:src/a.ts"), NodeKey.From("feature:a.one"), EdgeType.Implements, Provenance.Deterministic, 1.0, [])]);

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task The_hosts_eval_store_writes_both_reports_and_round_trips_the_as_of_map_cache()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await ContextHosts.Approved(_repo, sessions: false, Token);
        var store = host.App.Services.GetRequiredService<IEvalStore>();

        var backtest = await store.WriteReport(EvalReportKind.Backtest, Backtest(), _now, Token);
        var quality = await store.WriteReport(EvalReportKind.Map, Quality(), _now, Token);
        var wrote = await store.WriteMap("host-key", Map(), Token);
        var read = await store.ReadMap("host-key", Token);

        Assert.True(backtest.IsSuccess, backtest.IsSuccess ? "" : backtest.Error.Message);
        Assert.True(File.Exists(backtest.Value));
        Assert.True(quality.IsSuccess, quality.IsSuccess ? "" : quality.Error.Message);
        Assert.True(wrote.IsSuccess);
        Assert.Equal(["code:src/a.ts", "feature:a.one"], read.Value!.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal).ToList());
    }
}
