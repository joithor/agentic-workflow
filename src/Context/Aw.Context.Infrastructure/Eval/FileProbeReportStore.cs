using System.Text.Json;
using Aw.Context.Core.Eval;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>The probe reports folder (stub).</summary>
internal sealed class FileProbeReportStore(ContextStoreSettings settings, JsonSerializerOptions options) : IProbeReportStore
{
    public ValueTask<Result<ProbeReport?>> Latest(string probeFileHash, CancellationToken cancellationToken) => throw new NotSupportedException($"{settings.Home} {options.WriteIndented}");

    public ValueTask<Result<string>> Write(ProbeReport report, DateTimeOffset runAt, CancellationToken cancellationToken) => throw new NotSupportedException();
}
