using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// The eval state in files under $AW_SINDRI_HOME/context/eval (0700; files 0600 by temp file and rename): reports in eval/backtest and eval/map,
/// the as-of map cache in eval/cache/maps, and the purge. Nothing is written when the context home is inside a git worktree (SND-CONTEXT-005).
/// A file past <see cref="TimeToLive"/> by its modified time is expired.
/// </summary>
internal sealed class FileEvalStore(ContextStoreSettings settings) : IEvalStore
{
    /// <summary>How long eval files are kept (spec §9.1).</summary>
    internal static readonly TimeSpan TimeToLive = TimeSpan.FromDays(90);

    public ValueTask<Result<string>> WriteReport<T>(EvalReportKind kind, T report, DateTimeOffset at, CancellationToken cancellationToken)
        where T : class
    {
        var (folder, prefix) = kind == EvalReportKind.Backtest ? (settings.BacktestDirectory, "backtest-") : (settings.MapQualityDirectory, "map-");
        var path = Path.Combine(folder, prefix + at.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".json");
        return ValueTask.FromResult(Write(path, AwJson.Serialize(report, EvalFileJson.Options)).Map(_ => path));
    }

    public async ValueTask<Result<ExtractedMap?>> ReadMap(string key, CancellationToken cancellationToken)
    {
        var path = CachePath(key);
        if (!File.Exists(path))
        {
            return Results.Success<ExtractedMap?>(value: null);
        }

        try
        {
            var read = AwJson.Deserialize<ExtractedMap>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), EvalFileJson.Options);
            return Results.Success(read.IsSuccess ? read.Value : null);   // a file that doesn't parse is rebuilt
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{path} can't be read: {e.Message}");
        }
    }

    public ValueTask<Result<Unit>> WriteMap(string key, ExtractedMap map, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Write(CachePath(key), AwJson.Serialize(map, EvalFileJson.Options)));

    public ValueTask<Result<EvalPurgeReport>> Purge(DateTimeOffset now, bool all, CancellationToken cancellationToken) => ValueTask.FromResult(Sweep(now, all, delete: true));

    public ValueTask<Result<EvalPurgeReport>> Expired(DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult(Sweep(now, all: false, delete: false));

    private string CachePath(string key) =>
        Path.Combine(settings.MapCacheDirectory, new string([.. key.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_')]) + ".json");

    private Result<Unit> Write(string path, byte[] contents)
    {
        if (Worktrees.Containing(Path.Combine(settings.Home, "x")) is { } worktree)
        {
            return ContextErrors.PathInWorktree.With($"{settings.Home} is inside the git worktree {worktree} (fix: point AW_SINDRI_HOME outside every repo)");
        }

        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.EnsureDirectory(settings.EvalDirectory);
            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(path)!);   // why: every path above has a parent folder
            if (path.StartsWith(settings.MapCacheDirectory, StringComparison.Ordinal))
            {
                OwnerOnly.EnsureDirectory(Path.GetDirectoryName(settings.MapCacheDirectory)!);   // why: the cache folder has a parent
                OwnerOnly.EnsureDirectory(settings.MapCacheDirectory);
            }

            OwnerOnly.WriteAtomically(path, contents);
            return Results.Ok();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{path} can't be written: {e.Message}");
        }
    }

    private Result<EvalPurgeReport> Sweep(DateTimeOffset now, bool all, bool delete)
    {
        try
        {
            var reports = Sweep([settings.BacktestDirectory, settings.MapQualityDirectory], now, all, delete);
            var cache = Sweep([settings.MapCacheDirectory], now, all, delete);
            var probes = all ? Sweep([settings.ProbesDirectory], now, all, delete) : 0;
            return new EvalPurgeReport(reports, cache, probes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{settings.EvalDirectory} can't be purged: {e.Message}");
        }
    }

    private static int Sweep(string[] folders, DateTimeOffset now, bool all, bool delete)
    {
        var count = 0;
        foreach (var file in folders.Where(Directory.Exists).SelectMany(f => Directory.EnumerateFiles(f, "*.json")))
        {
            if (!all && now - new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero) <= TimeToLive)
            {
                continue;
            }

            count++;
            if (delete)
            {
                File.Delete(file);
            }
        }

        return count;
    }
}
