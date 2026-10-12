using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Docs;

/// <summary>The repo-docs source (spec §5.2): its id and its record keys.</summary>
public static class RepoDocs
{
    /// <summary>The source id, 'repo-docs'.</summary>
    public static SourceId Id { get; } = SourceId.From("repo-docs");

    /// <summary>The canonical key of a repo document: 'repo:&lt;repo&gt;/&lt;path&gt;'.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The repo-relative path, '/'-separated.</param>
    /// <returns>The key.</returns>
    public static RecordKey Key(ContextRepoName repo, string path) => RecordKey.From(KeyText(repo, path));

    /// <summary>The canonical key of a repo document, when the path fits a <see cref="RecordKey"/>.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The repo-relative path, '/'-separated.</param>
    /// <param name="key">The key, when it fits.</param>
    /// <returns>Whether the path fits.</returns>
    public static bool TryKey(ContextRepoName repo, string path, out RecordKey key) => RecordKey.TryFrom(KeyText(repo, path), out key);

    /// <summary>The id of a chunk, '&lt;repo&gt;/&lt;path&gt;#L&lt;start&gt;-L&lt;end&gt;', when it fits an <see cref="ExternalId"/>.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The repo-relative path, '/'-separated.</param>
    /// <param name="start">The first line, 1-based.</param>
    /// <param name="end">The last line.</param>
    /// <param name="id">The id, when it fits.</param>
    /// <returns>Whether the path fits.</returns>
    public static bool TryChunkId(ContextRepoName repo, string path, int start, int end, out ExternalId id) =>
        ExternalId.TryFrom(string.Create(CultureInfo.InvariantCulture, $"{repo.Value}/{path}#L{start}-L{end}"), out id);

    private static string KeyText(ContextRepoName repo, string path) => $"repo:{repo.Value}/{path}";
}
