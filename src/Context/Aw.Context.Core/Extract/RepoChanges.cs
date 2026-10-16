namespace Aw.Context.Core.Extract;

/// <summary>What a repo changed from the merge base of two commits to the second one: the port's answer for the diff shape.</summary>
/// <param name="Base">The full sha of the first commit.</param>
/// <param name="Head">The full sha of the second commit.</param>
/// <param name="MergeBase">The full sha of their merge base.</param>
/// <param name="Files">The changed paths, ordinal order by path.</param>
public sealed record RepoChanges(string Base, string Head, string MergeBase, IReadOnlyList<RepoChange> Files);
