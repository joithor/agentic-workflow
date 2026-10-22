namespace Aw.Sindri.Core.Scoping;

/// <summary>A change as the scoping station reads it (scoping spec §4, C7): the diff against the merge base, its commits and its PR body.</summary>
/// <param name="Ref">The PR number or branch the scope input named.</param>
/// <param name="Base">The merge base's sha.</param>
/// <param name="Head">The head's sha.</param>
/// <param name="PullRequestBody">The PR body, or null for a branch with no PR.</param>
/// <param name="Commits">The commit messages between the base and the head, oldest first: untrusted text.</param>
/// <param name="Files">The changed paths that may be read, in order.</param>
/// <param name="Denied">The changed paths whose hunks were dropped because they are denied.</param>
/// <param name="Diff">The diff of the readable paths, cut at the read's cap: untrusted text.</param>
/// <param name="Truncated">Whether the cap cut the diff.</param>
public sealed record ChangeRecord(string Ref, string Base, string Head, string? PullRequestBody, IReadOnlyList<string> Commits, IReadOnlyList<string> Files, IReadOnlyList<string> Denied, string Diff, bool Truncated);
