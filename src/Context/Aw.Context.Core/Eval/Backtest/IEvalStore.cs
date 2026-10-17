using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>
/// The eval state in files under $AW_SINDRI_HOME/context/eval (0700, files 0600 by temp file and rename): backtest and map-quality reports,
/// the as-of map cache, and the 90-day purge. A path inside a git worktree is SND-CONTEXT-005; an unreadable or unwritable folder is SND-CONTEXT-004.
/// </summary>
public interface IEvalStore
{
    /// <summary>Writes a report to its folder, named by the time.</summary>
    /// <typeparam name="T">The report type (in the host's JSON context).</typeparam>
    /// <param name="kind">The folder.</param>
    /// <param name="report">The report.</param>
    /// <param name="at">The run's time (the file name).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The absolute path written.</returns>
    public ValueTask<Result<string>> WriteReport<T>(EvalReportKind kind, T report, DateTimeOffset at, CancellationToken cancellationToken)
        where T : class;

    /// <summary>Reads a cached as-of extraction.</summary>
    /// <param name="key">The <see cref="AsOfMapCache.Key(Engine.ContextRepoName, string, int, bool, RecognizerSet)"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The extraction, or null when there is none (or the file doesn't parse).</returns>
    public ValueTask<Result<ExtractedMap?>> ReadMap(string key, CancellationToken cancellationToken);

    /// <summary>Caches an as-of extraction.</summary>
    /// <param name="key">The <see cref="AsOfMapCache.Key(Engine.ContextRepoName, string, int, bool, RecognizerSet)"/>.</param>
    /// <param name="map">The extraction.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit.</returns>
    public ValueTask<Result<Unit>> WriteMap(string key, ExtractedMap map, CancellationToken cancellationToken);

    /// <summary>Deletes eval files older than 90 days, or all of them (probe reports included, which clears the probe baselines).</summary>
    /// <param name="now">Now.</param>
    /// <param name="all">True to delete everything.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>What was removed.</returns>
    public ValueTask<Result<EvalPurgeReport>> Purge(DateTimeOffset now, bool all, CancellationToken cancellationToken);

    /// <summary>Counts what <see cref="Purge"/> would remove past the TTL, deleting nothing.</summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The counts.</returns>
    public ValueTask<Result<EvalPurgeReport>> Expired(DateTimeOffset now, CancellationToken cancellationToken);
}
