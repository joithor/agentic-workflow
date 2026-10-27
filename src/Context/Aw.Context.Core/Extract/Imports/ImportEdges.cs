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
        var own = files.Where(f => !RepoPath.InDependencyFolder(f.Path)).ToList();
        var csharp = CsImports.Find(own, MaxNamespaceFiles);
        var pairs = TsImports.Find(own).Concat(csharp.Pairs)
            .Where(p => !string.Equals(p.From, p.To, StringComparison.Ordinal))
            .Distinct()
            .OrderBy(p => p.From, StringComparer.Ordinal).ThenBy(p => p.To, StringComparer.Ordinal)
            .ToList();
        return new ImportScan(pairs, csharp.SkippedNamespaces);
    }
}
