using System.Globalization;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Targets;

/// <summary>Reads the repo's .agentic-workflow/evidence.json, or the file --config names, and checks that no build output leads out of the repo.</summary>
internal sealed class FileEvidenceTargets(IRunFiles files, JsonSerializerOptions json, IExecutableLocator locator) : IEvidenceTargets
{
    /// <inheritdoc/>
    public async ValueTask<Result<ResolvedTargets>> LoadAsync(string repo, string? configPath, CancellationToken cancellationToken)
    {
        var path = configPath ?? RunPath.Combine(repo, TargetsFile.RepoRelativePath);
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error.Code != EvidenceErrors.FileNotFound.Code ? bytes.Error
                : configPath is null ? ResolvedTargets.Empty
                : bytes.Error with { Message = $"--config {path} does not exist" };
        }

        var read = TargetsReader.Read(bytes.Value, json);
        if (read.IsError)
        {
            return read.Error with { Message = $"targets {path}: {read.Error.Message}" };
        }

        return StaysInside(read.Value, repo, path);
    }

    // A build output that is a symlink out of the repo would let the repo's own config allow any program; one not built yet has nothing to check.
    private Result<ResolvedTargets> StaysInside(ResolvedTargets targets, string repo, string path)
    {
        var root = locator.RealDirectory(repo)?.TrimEnd('/');
        for (var i = 0; i < targets.BuildOutputs.Count; i++)
        {
            var real = root is null ? null : locator.RealDirectory(RunPath.Combine(repo, targets.BuildOutputs[i]));
            if (real is not null && !real.StartsWith(root + "/", StringComparison.Ordinal))
            {
                return EvidenceErrors.TargetsInvalid.With(string.Create(
                    CultureInfo.InvariantCulture, $"targets {path}: $.cli.buildOutputs[{i}]: '{targets.BuildOutputs[i]}' leads outside the repo ({real})"));
            }
        }

        return targets;
    }
}
