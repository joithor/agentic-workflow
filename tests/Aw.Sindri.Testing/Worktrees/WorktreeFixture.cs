using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>What a worktrees contract run works with.</summary>
/// <param name="Worktrees">The adapter under test.</param>
/// <param name="RepoRoot">A repo with two commits.</param>
/// <param name="Base">The repo's first commit, a full sha.</param>
/// <param name="OtherBase">The repo's second commit, a full sha.</param>
/// <param name="Parent">An absolute directory worktrees can go under.</param>
/// <param name="MakeDirty">Leaves an uncommitted file in the worktree at the path it is given.</param>
public sealed record WorktreeFixture(IWorktrees Worktrees, string RepoRoot, string Base, string OtherBase, string Parent, Func<string, Task> MakeDirty);
