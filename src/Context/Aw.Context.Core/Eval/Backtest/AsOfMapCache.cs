using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The as-of map cache's file key: repo, cut commit, extractor version and whether clusters were available, so a machine without graphify never reuses a clustered map.</summary>
public static class AsOfMapCache
{
    /// <summary>The key.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The cut commit (a full sha).</param>
    /// <param name="extractorVersion">The recognizers' version (<see cref="Extract.MapExtractor.Version"/>).</param>
    /// <param name="clustered">True when the cluster source answered.</param>
    /// <returns>'&lt;repo&gt;-&lt;sha12&gt;-x&lt;version&gt;-c&lt;0|1&gt;'.</returns>
    public static string Key(ContextRepoName repo, string commit, int extractorVersion, bool clustered)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{repo.Value}-{commit[..Math.Min(12, commit.Length)]}-x{extractorVersion}-c{(clustered ? 1 : 0)}");
    }

    /// <summary>A clustered key with the cluster pass's size limits: other limits can cluster other files, so they never reuse each other's map. The timeout is left out (a run that timed out is degraded and never cached). The shipped limits add nothing, so older entries still serve.</summary>
    /// <param name="key">A clustered key.</param>
    /// <param name="limits">The profile's limits.</param>
    /// <returns>The key, with '-g&lt;snapshot&gt;.&lt;file&gt;.&lt;graph&gt;' when those limits are not the shipped ones.</returns>
    public static string WithLimits(string key, ClusterLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        var shipped = ClusterLimits.Default;
        return (limits.MaxSnapshotMB, limits.MaxFileMB, limits.MaxGraphMB) == (shipped.MaxSnapshotMB, shipped.MaxFileMB, shipped.MaxGraphMB)
            ? key
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{key}-g{limits.MaxSnapshotMB}.{limits.MaxFileMB}.{limits.MaxGraphMB}");
    }

    /// <summary>The key of an extraction that also ran the profile's recognizers: the plain key and, when the set is not empty, its hash, so a changed recognizer set never reuses a cached map.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The cut commit (a full sha).</param>
    /// <param name="extractorVersion">The recognizers' version.</param>
    /// <param name="clustered">True when the cluster source answered.</param>
    /// <param name="recognizers">The profile's recognizers.</param>
    /// <returns>The plain key when the set is empty, else that key and '-&lt;hash&gt;'.</returns>
    public static string Key(ContextRepoName repo, string commit, int extractorVersion, bool clustered, Extract.RecognizerSet recognizers) =>
        recognizers.Hash.Length == 0 ? Key(repo, commit, extractorVersion, clustered) : $"{Key(repo, commit, extractorVersion, clustered)}-{recognizers.Hash}";
}
