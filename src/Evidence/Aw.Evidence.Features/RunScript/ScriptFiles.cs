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
    internal const string SummaryFile = "summary.json";

    internal async ValueTask<Result<LoadedScript>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes.Bind(b => ScriptReader.Read(b, json).Match<Result<LoadedScript>>(
            script => new LoadedScript(script, ContentHash.Sha256Hex(b)),
            error => error with { Message = $"script {path}: {error.Message}" }));
    }

    internal ValueTask<Result<Unit>> CreateRunDirectoryAsync(string runDirectory, CancellationToken cancellationToken) =>
        files.CreateDirectoryAsync(runDirectory, cancellationToken);

    internal ValueTask<Result<Unit>> WriteSummaryAsync(string runDirectory, RunSummary summary, CancellationToken cancellationToken) =>
        files.WriteAsync(RunPath.Combine(runDirectory, SummaryFile), AwJson.Serialize(summary, json), cancellationToken);
}
