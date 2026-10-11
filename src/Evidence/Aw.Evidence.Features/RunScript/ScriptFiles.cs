using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Features.RunScript;

/// <summary>The run's own files: the script it reads and the summary it writes.</summary>
internal sealed class ScriptFiles(IRunFiles files, JsonSerializerOptions json)
{
    internal async ValueTask<Result<LoadedScript>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes.Bind(b => ScriptReader.Read(b, json).Match<Result<LoadedScript>>(
            script => new LoadedScript(script, ContentHash.Sha256Hex(b)),
            error => error with { Message = $"script {path}: {error.Message}" }));
    }

    // A typo'd --baseline is a bad input: say so before any lock, rather than skipping the compare after the whole run.
    internal async ValueTask<Result<Unit>> CheckBaselineAsync(string? baseline, CancellationToken cancellationToken)
    {
        if (baseline is null)
        {
            return Unit.Value;
        }

        var size = await files.SizeAsync(baseline, cancellationToken).ConfigureAwait(false);
        return size.IsError ? size.Error with { Message = $"--baseline {baseline}: {size.Error.Message}" } : Unit.Value;
    }

    // A run that errors must not leave an earlier run's summary behind as if it were this run's evidence.
    internal async ValueTask<Result<Unit>> PrepareRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var created = await files.CreateDirectoryAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        return created.IsError ? created : await files.DeleteAsync(RunPath.Combine(runDirectory, RunSummary.FileName), cancellationToken).ConfigureAwait(false);
    }

    internal ValueTask<Result<Unit>> WriteSummaryAsync(string runDirectory, RunSummary summary, CancellationToken cancellationToken) =>
        files.WriteAsync(RunPath.Combine(runDirectory, RunSummary.FileName), AwJson.Serialize(summary, json), cancellationToken);
}
