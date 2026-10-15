using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract;

/// <summary>Composes the recognizers over a repo's files at one commit (S-4): deterministic nodes, each with a file-level 'implements' edge from the file that defines it.</summary>
public static class MapExtractor
{
    /// <summary>Extracts the map from the files.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <returns>The nodes and edges, ordered, the same for the same files.</returns>
    public static ExtractedMap Extract(ContextRepoName repo, IReadOnlyList<RepoFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _ = repo;
        throw new NotSupportedException("S-4: MapExtractor.Extract is not built yet");
    }
}
