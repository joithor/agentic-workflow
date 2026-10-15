#pragma warning disable MA0182 // red stub: used by the implementation
namespace Aw.Meta.Core.Worktrees;

/// <summary>The rules that decide whether a worktree may be removed: all of them must hold, and age is never one.</summary>
internal static class WorktreeRules
{
    /// <summary>Judges one worktree.</summary>
    /// <param name="facts">What the inspector found.</param>
    /// <returns>Removable when no rule fails; otherwise kept, with every failing rule's reason.</returns>
    internal static WorktreeVerdict Judge(WorktreeFacts facts) => throw new NotSupportedException();
}
