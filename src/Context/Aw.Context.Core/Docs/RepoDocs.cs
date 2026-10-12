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
    public static RecordKey Key(string repo, string path) => RecordKey.From($"repo:{repo}/{path}");
}
