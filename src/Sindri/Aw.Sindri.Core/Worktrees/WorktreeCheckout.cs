namespace Aw.Sindri.Core.Worktrees;

/// <summary>A worktree that exists.</summary>
/// <param name="Path">Its absolute path.</param>
/// <param name="Head">The commit it is at, a full sha.</param>
/// <param name="Branch">The branch it has checked out.</param>
public sealed record WorktreeCheckout(string Path, string Head, string Branch);
