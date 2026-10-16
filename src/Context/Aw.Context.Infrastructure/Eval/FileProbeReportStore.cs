using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// The probe reports folder, $AW_SINDRI_HOME/context/eval/probes/ (0700): one JSON file per run, 'probes-&lt;time&gt;.json',
/// written by temp file and rename at 0600. The baseline of a probe file is the newest report carrying its hash that was
/// complete and did not regress; a report that doesn't parse is skipped.
/// </summary>
internal sealed class FileProbeReportStore(ContextStoreSettings settings) : IProbeReportStore
{
    public async ValueTask<Result<ProbeReport?>> Baseline(string probeFileHash, CancellationToken cancellationToken)
    {
        var folder = settings.ProbesDirectory;
        if (!Directory.Exists(folder))
        {
            return Results.Success<ProbeReport?>(value: null);
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "probes-*.json").Order(StringComparer.Ordinal).Reverse())
            {
                var read = AwJson.Deserialize<ProbeReport>(WithBaselineName(await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false)), EvalFileJson.Options);
                if (read.IsSuccess && read.Value.IsBaseline && string.Equals(read.Value.ProbeFileHash, probeFileHash, StringComparison.Ordinal))
                {
                    return read.Value;
                }
            }

            return Results.Success<ProbeReport?>(value: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{folder} can't be read: {e.Message}");
        }
    }

    /// <summary>A report written before 'previous' was renamed 'baseline' reads as the new name.</summary>
    private static byte[] WithBaselineName(byte[] bytes)
    {
        try
        {
            if (JsonNode.Parse(bytes) is JsonObject report && !report.ContainsKey("baseline") && report.Remove("previous", out var legacy))
            {
                report["baseline"] = legacy;
                return System.Text.Encoding.UTF8.GetBytes(report.ToJsonString());
            }
        }
        catch (JsonException)
        {
            // not JSON: the deserializer below reports it
        }

        return bytes;
    }

    public ValueTask<Result<string>> Write(ProbeReport report, DateTimeOffset runAt, CancellationToken cancellationToken)
    {
        var path = Path.Combine(settings.ProbesDirectory, "probes-" + runAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".json");
        if (Worktrees.Containing(Path.Combine(settings.Home, "x")) is { } worktree)
        {
            return ValueTask.FromResult<Result<string>>(ContextErrors.PathInWorktree.With($"{settings.Home} is inside the git worktree {worktree} (fix: point AW_SINDRI_HOME outside every repo)"));
        }

        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.EnsureDirectory(settings.EvalDirectory);
            OwnerOnly.EnsureDirectory(settings.ProbesDirectory);
            OwnerOnly.WriteAtomically(path, AwJson.Serialize(report, EvalFileJson.Options));
            return ValueTask.FromResult(Results.Success(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<string>>(ContextErrors.StoreUnavailable.With($"{path} can't be written: {e.Message}"));
        }
    }
}
