using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>The JSON-file adapter of <see cref="IRunHistory"/>: one file per run under
/// &lt;state&gt;/readiness/runs/&lt;repo&gt;/&lt;scope&gt;/&lt;UTC time&gt;-NNN.json, written to a temp file and moved into place. It writes
/// nowhere else (spec §4c: Readiness owns only readiness/).</summary>
/// <param name="settings">Where the state directory is.</param>
/// <param name="json">The package's JSON options (AwJson over its one context), registered by Composition.</param>
internal sealed class JsonRunHistory(ReadinessSettings settings, JsonSerializerOptions json) : IRunHistory
{
    public async ValueTask<Result<RunRecord?>> Latest(RepoName repo, string scope, CancellationToken cancellationToken)
    {
        var directory = DirectoryOf(repo, scope);
        try
        {
            var newest = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).LastOrDefault()
                : null;
            if (newest is null)
            {
                return Results.Success<RunRecord?>(value: null);
            }

            var bytes = await File.ReadAllBytesAsync(newest, cancellationToken).ConfigureAwait(false);
            var run = AwJson.Deserialize<RunRecord>(bytes, json);
            return run.IsSuccess
                ? Results.Success<RunRecord?>(run.Value)
                : Down($"{newest} is not a readable run ({run.Error.Message}); fix: move it out of {directory}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Down($"can't read {directory}: {e.Message}; fix: check its permissions");
        }
    }

    public async ValueTask<Result<string>> Save(RunRecord run, CancellationToken cancellationToken)
    {
        var directory = DirectoryOf(run.Repo, run.Scope);
        try
        {
            _ = Directory.CreateDirectory(directory);
            var stem = run.RecordedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
            var path = FreePath(directory, stem);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, AwJson.Serialize(run, json), cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: false);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Results.Failure<string>(ReadinessErrors.HistoryUnavailable.With($"can't write {directory}: {e.Message}; fix: check its permissions or set AW_STATE_DIR"));
        }
    }

    private string DirectoryOf(RepoName repo, string scope) =>
        scope.Length > 0 && scope.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            ? Path.Combine(settings.StateDirectory, "runs", repo.Value, scope)
            : throw new ArgumentException($"scope '{scope}' must be a-z, 0-9 and '-'", nameof(scope));

    private static string FreePath(string directory, string stem)
    {
        // A sequence suffix keeps runs at the same instant apart and in order (ordinal name order is save order).
        string path;
        for (var n = 0; File.Exists(path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{stem}-{n:D3}.json"))); n++)
        {
        }

        return path;
    }

    private static Result<RunRecord?> Down(string message) =>
        Results.Failure<RunRecord?>(ReadinessErrors.HistoryUnavailable.With(message));
}
