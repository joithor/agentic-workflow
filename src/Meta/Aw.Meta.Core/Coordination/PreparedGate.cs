using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Github;

namespace Aw.Meta.Core.Coordination;

/// <summary>A pull request ready to gate: its head merged onto main in a worktree (or found to conflict).</summary>
/// <param name="Pr">The pull request as GitHub reports it.</param>
/// <param name="Main">The sha of origin/main the tree was built on.</param>
/// <param name="Tree">The worktree's directory.</param>
/// <param name="Merged">False when the head conflicts with main.</param>
/// <param name="Changed">The paths the pull request changes against main; empty unless asked for.</param>
public sealed record PreparedGate(PrDetails Pr, string Main, string Tree, bool Merged, IReadOnlyList<string> Changed)
{
    /// <summary>The git tree sha of the worktree after the merge (HEAD^{tree}); null when the head conflicted.</summary>
    public string? TreeSha { get; init; }

    /// <summary>For a train gate whose head conflicted: the pull request ahead of it that it conflicts with; null otherwise.</summary>
    public StackedPr? ConflictsWith { get; init; }
}
