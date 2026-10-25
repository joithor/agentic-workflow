using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Control;

/// <summary>A directory inside a git repository, against a list of profile repos.</summary>
/// <param name="Path">The repository's top level, as a real path.</param>
/// <param name="Repo">The entry whose path is that top level or the repository's main checkout (a worktree of a repo with no main checkout matches by its own top level only), or null when none is.</param>
public sealed record RepoPlacement(string Path, RepoName? Repo);
