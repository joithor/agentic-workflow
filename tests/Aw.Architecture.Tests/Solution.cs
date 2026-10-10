using System.Reflection;
using System.Xml.Linq;

namespace Aw.Architecture.Tests;

/// <summary>The projects AgenticWorkflow.slnx lists, read from the source tree.</summary>
internal static class Solution
{
    /// <summary>The repo root, recorded at build time (AwRepoRoot assembly metadata).</summary>
    public static string RepoRoot { get; } = typeof(Solution).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "AwRepoRoot").Value ?? throw new InvalidOperationException("AwRepoRoot metadata is empty");

    /// <summary>Every project path in the solution, repo-relative.</summary>
    public static IReadOnlyList<string> ProjectPaths { get; } =
        [.. XDocument.Load(Path.Combine(RepoRoot, "AgenticWorkflow.slnx")).Descendants("Project")
            .Select(p => (string?)p.Attribute("Path") ?? string.Empty)];

    /// <summary>The project name of a path (the .csproj file name).</summary>
    /// <param name="path">A repo-relative project path.</param>
    /// <returns>The project name.</returns>
    public static string NameOf(string path) => Path.GetFileNameWithoutExtension(path);

    /// <summary>Production projects: under src/, named Aw.*.</summary>
    public static IReadOnlyList<string> ProductionProjects { get; } =
        [.. ProjectPaths.Where(p => p.StartsWith("src/", StringComparison.Ordinal)).Select(NameOf)
            .Where(n => n.StartsWith("Aw.", StringComparison.Ordinal))];
}
