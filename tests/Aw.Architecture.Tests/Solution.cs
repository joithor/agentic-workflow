using System.Reflection;
using System.Xml.Linq;

namespace Aw.Architecture.Tests;

/// <summary>
/// The projects AgenticWorkflow.slnx lists, read from the source tree. Projects under src/ that the solution doesn't
/// list (the AOT smoke apps, src/Aw.Kernel.AotSmoke and src/Aw.Kernel.Data.AotSmoke, which the AOT CI job publishes)
/// are out of scope here.
/// </summary>
internal static class Solution
{
    /// <summary>
    /// The repo root, recorded at build time (AwRepoRoot assembly metadata). It is an absolute path, so the registry
    /// tests run only from the checkout that built them, which is how CI and local runs use them.
    /// </summary>
    public static string RepoRoot { get; } = typeof(Solution).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "AwRepoRoot").Value ?? throw new InvalidOperationException("AwRepoRoot metadata is empty");

    /// <summary>Every project path in the solution, repo-relative.</summary>
    public static IReadOnlyList<string> ProjectPaths { get; } =
        [.. XDocument.Load(Path.Combine(RepoRoot, "AgenticWorkflow.slnx")).Descendants("Project")
            .Select(p => (string?)p.Attribute("Path") ?? string.Empty)];

    /// <summary>
    /// Production projects: under src/, named Aw.*, except analyzers (*.Analyzers runs in the compiler, not at run
    /// time; deps.yaml marks it analyzer: true).
    /// </summary>
    public static IReadOnlyList<string> ProductionProjects { get; } = ProductionOf(ProjectPaths);

    /// <summary>The production project names among solution project paths.</summary>
    /// <param name="paths">Repo-relative project paths.</param>
    /// <returns>Project names under src/, named Aw.*, that aren't analyzers.</returns>
    public static IReadOnlyList<string> ProductionOf(IEnumerable<string> paths) =>
        [.. paths.Where(p => p.StartsWith("src/", StringComparison.Ordinal)).Select(NameOf)
            .Where(n => n.StartsWith("Aw.", StringComparison.Ordinal) && !n.EndsWith(".Analyzers", StringComparison.Ordinal))];

    /// <summary>The project name of a path (the .csproj file name).</summary>
    /// <param name="path">A repo-relative project path.</param>
    /// <returns>The project name.</returns>
    public static string NameOf(string path) => Path.GetFileNameWithoutExtension(path);

    /// <summary>The assembly a solution project builds: its &lt;AssemblyName&gt;, else the project name.</summary>
    /// <param name="project">The project name, e.g. Aw.Judge.Host.</param>
    /// <returns>The assembly name (e.g. judge for a Host that renames its binary), or the project name when the project isn't in the solution.</returns>
    public static string AssemblyOf(string project)
    {
        var path = ProjectPaths.FirstOrDefault(p => NameOf(p) == project);
        return path is null ? project : AssemblyNameIn(XDocument.Load(Path.Combine(RepoRoot, path)), project);
    }

    /// <summary>The root namespace of a solution project: its &lt;RootNamespace&gt;, else the project name.</summary>
    /// <param name="project">The project name, e.g. Aw.Sindri.Workflows.</param>
    /// <returns>The root namespace, or the project name when the project isn't in the solution or sets none.</returns>
    public static string RootNamespaceOf(string project)
    {
        var path = ProjectPaths.FirstOrDefault(p => NameOf(p) == project);
        return path is null ? project : XDocument.Load(Path.Combine(RepoRoot, path)).Descendants("RootNamespace")
            .Select(e => e.Value.Trim()).LastOrDefault(v => v.Length > 0) ?? project;
    }

    /// <summary>The &lt;AssemblyName&gt; a project file sets, else the project name.</summary>
    /// <param name="projectFile">The parsed .csproj.</param>
    /// <param name="project">The project name.</param>
    /// <returns>The assembly name.</returns>
    public static string AssemblyNameIn(XDocument projectFile, string project) =>
        projectFile.Descendants("AssemblyName").Select(e => e.Value.Trim()).LastOrDefault(v => v.Length > 0) ?? project;
}
