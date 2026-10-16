using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>One changed path of a diff range.</summary>
/// <param name="Path">The repo-relative path, '/'-separated.</param>
/// <param name="Change">How it changed.</param>
public sealed record RepoChange(string Path, ChangeKind Change);
