using Aw.Context.Core.Eval;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EvalProbes;

/// <summary>Reads the probe file, finds the previous run of the same file, runs the probes and writes the report (spec §9.2 item 3).</summary>
internal sealed class EvalProbesService(IProbeFileReader files, IProbeReportStore reports, TimeProvider clock) : IService<EvalProbesRequest, ProbeRun>
{
    public async ValueTask<Result<ProbeRun>> Handle(EvalProbesRequest request, CancellationToken cancellationToken)
    {
        var file = await files.Read(request.File, cancellationToken).ConfigureAwait(false);
        var previous = await file.BindAsync(f => reports.Latest(f.Hash, cancellationToken)).ConfigureAwait(false);
        if (previous.IsError)
        {
            return previous.Error;
        }

        var runAt = clock.GetUtcNow();
        var report = await ProbeRunner.Run(file.Value, request.Ask, previous.Value, runAt, cancellationToken).ConfigureAwait(false);
        var written = await report.BindAsync(r => reports.Write(r, runAt, cancellationToken)).ConfigureAwait(false);
        return written.IsError ? written.Error : new ProbeRun(report.Value, written.Value);
    }
}
