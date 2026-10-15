using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Targets;

/// <summary>Reads the repo's .agentic-workflow/evidence.json, or the file --config names.</summary>
internal sealed class FileEvidenceTargets : IEvidenceTargets
{
    /// <inheritdoc/>
    public ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken) => throw new NotSupportedException();
}
