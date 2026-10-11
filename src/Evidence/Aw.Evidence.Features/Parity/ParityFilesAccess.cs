using System.Text.Json;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Features.Parity;

/// <summary>The parity run's own files: the manifest it reads and the parity.json it replaces.</summary>
internal sealed class ParityFilesAccess(IRunFiles files, JsonSerializerOptions json)
{
    internal async ValueTask<Result<DesignManifest>> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes.Bind(b => ManifestReader.Read(b, json).Match<Result<DesignManifest>>(
            static manifest => manifest,
            error => error with { Message = $"manifest {path}: {error.Message}" }));
    }

    // A failed run must not leave an earlier run's summary for parity-plan to publish.
    internal async ValueTask<Result<Unit>> PrepareAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, ParitySummary.FileName), cancellationToken).ConfigureAwait(false);
    }

    internal ValueTask<Result<Unit>> WriteSummaryAsync(string runDirectory, ParitySummary summary, CancellationToken cancellationToken) =>
        files.WriteAsync(RunPath.Combine(runDirectory, ParitySummary.FileName), AwJson.Serialize(summary, json), cancellationToken);
}
