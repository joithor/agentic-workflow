using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Kernel;

namespace Aw.Context.Tests.EvalProbes;

/// <summary>The probe ports of a world that has no probe file and no reports (the map and query tests never run probes).</summary>
internal sealed class NoProbes : IProbeFileReader, IProbeReportStore
{
    public ValueTask<Result<ProbeFile>> Read(string? path, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProbeFile>>(ContextErrors.NoProbeFile.With("this test world has no probe file"));

    public ValueTask<Result<ProbeReport?>> Latest(string probeFileHash, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProbeReport?>>((ProbeReport?)null);

    public ValueTask<Result<string>> Write(ProbeReport report, DateTimeOffset runAt, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>("/no-probes/report.json");
}
