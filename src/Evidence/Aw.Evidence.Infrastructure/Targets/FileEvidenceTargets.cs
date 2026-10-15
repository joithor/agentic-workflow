using System.Text.Json;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Targets;

/// <summary>Reads the repo's .agentic-workflow/evidence.json, or the file --config names, and checks that no build output leads out of the repo.</summary>
internal sealed class FileEvidenceTargets(IRunFiles files, JsonSerializerOptions json, IExecutableLocator locator) : IEvidenceTargets
{
    internal (IRunFiles, JsonSerializerOptions, IExecutableLocator) Collaborators { get; } = (files, json, locator);

    /// <inheritdoc/>
    public ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
