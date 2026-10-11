namespace Aw.BugFix.Core.Model;

/// <summary>A committed check file, located inside its repository.</summary>
/// <param name="RepoPath">The path relative to the repository root, with '/'.</param>
/// <param name="FullPath">The resolved (real) full path.</param>
public sealed record CheckFile(string RepoPath, string FullPath);
