using Aw.Context.Core.Extract;

namespace Aw.Context.Testing.Extract;

/// <summary>
/// The history a reader's repo holds for the diff shape's contract tests: 'main' moved on after 'feature' branched off,
/// and 'feature' then renamed a file. Every reader answers the same questions over it.
/// </summary>
/// <param name="Base">The tip of 'main' (a commit 'feature' does not contain).</param>
/// <param name="Head">The tip of 'feature' before the rename.</param>
/// <param name="MergeBase">Where 'feature' branched off.</param>
/// <param name="Expected">What 'feature' changed from the merge base, ordinal order by path.</param>
/// <param name="Unrelated">A commit with no history in common with the others.</param>
/// <param name="RenameHead">The commit after <paramref name="Head"/> that renamed a file (its parent is <paramref name="Head"/>).</param>
/// <param name="RenameExpected">What the rename changed: the new path added and the old one deleted.</param>
public sealed record ChangeWorld(string Base, string Head, string MergeBase, IReadOnlyList<RepoChange> Expected, string Unrelated, string RenameHead, IReadOnlyList<RepoChange> RenameExpected)
{
    /// <summary>A sha-shaped value no commit of the world has.</summary>
    public const string Absent = "0123456789abcdef0123456789abcdef01234567";
}
