using System.Text.Json;
using Aw.Context.Core.Eval;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Reads a probe file (stub).</summary>
internal sealed class ProbeFileReader(ContextStoreSettings settings, JsonSerializerOptions options) : IProbeFileReader
{
    public ValueTask<Result<ProbeFile>> Read(string? path, CancellationToken cancellationToken) => throw new NotSupportedException($"{settings.Home} {options.WriteIndented}");
}
