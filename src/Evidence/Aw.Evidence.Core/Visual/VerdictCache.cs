using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;

namespace Aw.Evidence.Core.Visual;

/// <summary>
/// The verdict cache: the judge's opinion reused, never reality. Every browser step and pixel check still runs; nothing
/// here can turn a behaviour failure into a pass. Immutable: <see cref="With"/> returns a new cache.
/// </summary>
/// <param name="Entries">Cached verdicts by key.</param>
public sealed record VerdictCache(IReadOnlyDictionary<string, CachedVerdict> Entries)
{
    /// <summary>An empty cache.</summary>
    public static VerdictCache Empty { get; } = new(new Dictionary<string, CachedVerdict>(StringComparer.Ordinal));

    /// <summary>
    /// A hit needs the identical key. With an unknown app build an entry is trusted only inside the run that wrote it
    /// (identical pixels can hide a new build).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="appBuildKnown">Whether this run knows its app build.</param>
    /// <param name="runId">This run.</param>
    /// <returns>The cached verdict, or null.</returns>
    public CachedVerdict? Lookup(string key, bool appBuildKnown, RunId runId) =>
        Entries.TryGetValue(key, out var entry) && (appBuildKnown || entry.RunId.Value == runId.Value) ? entry : null;

    /// <summary>A cache with <paramref name="verdict"/> stored under <paramref name="key"/> for <paramref name="runId"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="verdict">The verdict.</param>
    /// <param name="runId">The run that asked.</param>
    /// <returns>The new cache.</returns>
    public VerdictCache With(string key, VisualVerdict verdict, RunId runId) =>
        new(new Dictionary<string, CachedVerdict>(Entries, StringComparer.Ordinal) { [key] = new(verdict.Decision, verdict.Reasons, runId) });
}
