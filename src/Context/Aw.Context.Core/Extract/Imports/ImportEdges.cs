namespace Aw.Context.Core.Extract.Imports;

/// <summary>Reads the imports of a repo's files and resolves each to a file of the same repo.</summary>
internal static class ImportEdges
{
    /// <summary>A C# namespace declared in more files than this makes no edges.</summary>
    internal const int MaxNamespaceFiles = 50;

    /// <summary>Finds the resolved imports.</summary>
    /// <param name="files">The repo's files.</param>
    /// <returns>The pairs and the skipped-namespace count.</returns>
    internal static ImportScan Find(IReadOnlyList<RepoFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return new ImportScan([], 0);
    }
}
