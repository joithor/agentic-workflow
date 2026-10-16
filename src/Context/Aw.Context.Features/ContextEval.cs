using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Results;
using Aw.Context.Features.EvalProbes;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The eval slices behind the facade (probes, backtest, map quality, purge).</summary>
internal sealed class ContextEval(
    IService<EvalProbesRequest, ProbeRun> probes,
    IService<BacktestRequest, BacktestRun> backtest,
    IService<MapQualityRequest, MapQualityRun> mapQuality,
    IService<EvalPurgeRequest, EvalPurgeReport> purge)
{
    public ValueTask<Result<ProbeRun>> Probes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken) =>
        probes.Handle(new EvalProbesRequest(file, ask), cancellationToken);

    public ValueTask<Result<BacktestRun>> Backtest(BacktestRequest request, CancellationToken cancellationToken) => backtest.Handle(request, cancellationToken);

    public ValueTask<Result<MapQualityRun>> Map(MapQualityRequest request, CancellationToken cancellationToken) => mapQuality.Handle(request, cancellationToken);

    public ValueTask<Result<EvalPurgeReport>> Purge(EvalPurgeRequest request, CancellationToken cancellationToken) => purge.Handle(request, cancellationToken);
}
