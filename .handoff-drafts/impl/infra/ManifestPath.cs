namespace Aw.CodeIndex.Infrastructure.Deps;

internal static class ManifestPath
{
    /// <summary>The last segment of a repo-relative path (git paths use '/').</summary>
    internal static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];
}
