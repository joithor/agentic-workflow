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
    /// A hit needs the identical key. With an unknown app build an entry is trusted only inside the run invocation that
    /// wrote it (identical pixels can hide a new build): a rerun in the same directory is a new invocation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="appBuildKnown">Whether this run knows its app build.</param>
    /// <param name="runInstance">This run invocation.</param>
    /// <returns>The cached verdict, or null.</returns>
    public CachedVerdict? Lookup(string key, bool appBuildKnown, RunInstanceId runInstance) =>
        Entries.TryGetValue(key, out var entry) && (appBuildKnown || entry.RunInstance.Value == runInstance.Value) ? entry : null;

    /// <summary>A cache with <paramref name="verdict"/> stored under <paramref name="key"/> for <paramref name="runInstance"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="verdict">The verdict.</param>
    /// <param name="runInstance">The run invocation that asked.</param>
    /// <returns>The new cache.</returns>
    public VerdictCache With(string key, VisualVerdict verdict, RunInstanceId runInstance) =>
        new(new Dictionary<string, CachedVerdict>(Entries, StringComparer.Ordinal) { [key] = new(verdict.Decision, verdict.Reasons, runInstance) });
}
