using Aw.Kernel;

namespace Aw.Context.Core.Eval;

/// <summary>The probe reports folder, $AW_SINDRI_HOME/context/eval/probes/ (0700, files 0600 by temp file and rename).</summary>
public interface IProbeReportStore
{
    /// <summary>The newest report of a run of the probe file with <paramref name="probeFileHash"/>.</summary>
    /// <param name="probeFileHash">The probe file's hash.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The report, null when there is none, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<ProbeReport?>> Latest(string probeFileHash, CancellationToken cancellationToken);

    /// <summary>Writes <paramref name="report"/>, named by <paramref name="runAt"/>.</summary>
    /// <param name="report">The report.</param>
    /// <param name="runAt">The run's time (its file name).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The absolute path written, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<string>> Write(ProbeReport report, DateTimeOffset runAt, CancellationToken cancellationToken);
}
