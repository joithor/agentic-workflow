namespace Aw.Meta.Core.Worktrees;

/// <summary>Which worktrees an inspection looks at: all of them, or those that belong to some pull requests (their head branches and their gate trees).</summary>
public sealed record WorktreeScope
{
    private WorktreeScope(IReadOnlySet<int>? prs)
    {
        Prs = prs;
    }

    /// <summary>Every worktree of the repo.</summary>
    public static WorktreeScope All { get; } = new((IReadOnlySet<int>?)null);

    /// <summary>The pull requests, or null for every worktree.</summary>
    public IReadOnlySet<int>? Prs { get; }

    /// <summary>The worktrees of some pull requests.</summary>
    /// <param name="prs">The pull request numbers.</param>
    /// <returns>The scope.</returns>
    public static WorktreeScope ForPrs(IEnumerable<int> prs)
    {
        ArgumentNullException.ThrowIfNull(prs);
        return new WorktreeScope(prs.ToHashSet());
    }
}
