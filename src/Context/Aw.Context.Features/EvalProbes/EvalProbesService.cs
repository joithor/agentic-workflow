using Aw.Context.Core.Eval;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalProbes;

/// <summary>Reads the probe file, finds the baseline run of the same file, runs the probes and writes the report (not for an incomplete run) (spec §9.2 item 3).</summary>
internal sealed class EvalProbesService(IProbeFileReader files, IProbeReportStore reports, TimeProvider clock) : IService<EvalProbesRequest, ProbeRun>
{
    public async ValueTask<Result<ProbeRun>> Handle(EvalProbesRequest request, CancellationToken cancellationToken)
    {
        var file = await files.Read(request.File, cancellationToken).ConfigureAwait(false);
        var baseline = await file.BindAsync(f => reports.Baseline(f.Hash, cancellationToken)).ConfigureAwait(false);
        if (baseline.IsError)
        {
            return baseline.Error;
        }

        var runAt = clock.GetUtcNow();
        var report = await ProbeRunner.Run(file.Value, request.Ask, baseline.Value, runAt, cancellationToken).ConfigureAwait(false);
        if (report.IsError || report.Value.Incomplete)
        {
            return report.IsError ? report.Error : new ProbeRun(report.Value, Path: null);   // an incomplete run is never saved, so it can't become a baseline
        }

        var written = await reports.Write(report.Value, runAt, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new ProbeRun(report.Value, written.Value);
    }
}
