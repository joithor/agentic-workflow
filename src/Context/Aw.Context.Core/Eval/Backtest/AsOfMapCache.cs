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
