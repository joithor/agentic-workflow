using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Core.Criteria;

/// <summary>What a check reads: the checkout, the host facts and the run's time, plus the lookups checks share.</summary>
/// <param name="Tree">The checkout.</param>
/// <param name="Host">The host facts.</param>
/// <param name="Now">The run's time.</param>
internal sealed record RepoContext(IRepoTree Tree, HostFacts Host, DateTimeOffset Now)
{
    internal const string WorkflowDirectory = ".github/workflows";

    internal bool Has(string path) => Tree.Exists(path);

    internal bool Contains(string path, string needle) =>
        Tree.ReadText(path)?.Contains(needle, StringComparison.Ordinal) == true;

    /// <summary>The first path that exists, or null.</summary>
    internal string? FirstExisting(params string[] paths) => paths.FirstOrDefault(Has);

    /// <summary>The files under a directory whose text contains the needle.</summary>
    internal List<string> FilesContaining(string directory, string suffix, string needle) =>
        [.. Tree.Files(directory, suffix).Where(f => Contains(f, needle))];

    /// <summary>The CI workflows (.yml and .yaml) whose text contains the needle.</summary>
    internal List<string> WorkflowsContaining(string needle) =>
        [.. FilesContaining(WorkflowDirectory, ".yml", needle), .. FilesContaining(WorkflowDirectory, ".yaml", needle)];

    /// <summary>"present in A, B" or "no X found", for evidence.</summary>
    internal static string Named(IReadOnlyCollection<string> paths, string what) =>
        paths.Count == 0 ? $"no {what} found" : $"{what}: {string.Join(", ", paths.Take(3))}{(paths.Count > 3 ? ", …" : "")}";
}
