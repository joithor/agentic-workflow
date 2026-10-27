using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract.DataStores;

/// <summary>Finds the data stores a repo declares and the code files that read or write them (deterministic things, with reads and writes edges).</summary>
public static class DataStorePass
{
    /// <summary>Runs the adapters over the files.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <param name="adapters">The adapters.</param>
    /// <param name="schemaGlobs">The globs of the files that declare stores.</param>
    /// <returns>The things, the code files that use them and the edges, ordered.</returns>
    public static ExtractedMap Run(ContextRepoName repo, IReadOnlyList<RepoFile> files, IReadOnlyList<IDataStoreAdapter> adapters, IReadOnlyList<string> schemaGlobs)
    {
        _ = repo.Value;   // stub: the real pass joins it into the evidence keys
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(schemaGlobs);
        return ExtractedMap.Empty;
    }
}
