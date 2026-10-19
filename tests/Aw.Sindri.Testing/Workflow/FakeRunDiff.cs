using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IRunDiff"/> in memory: a worktree is a HEAD name and a dirty flag the test sets. Held to <see cref="RunDiffContract"/>.</summary>
public sealed class FakeRunDiff : IRunDiff
{
    private readonly Dictionary<string, (string Head, bool Dirty)> _worktrees = new(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="worktree"/> exist at <paramref name="head"/>, clean.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="head">Its HEAD.</param>
    public void Add(string worktree, string head = "c1") => _worktrees[worktree] = (head, false);

    /// <summary>Leaves an uncommitted change in <paramref name="worktree"/>.</summary>
    /// <param name="worktree">The path.</param>
    public void Touch(string worktree) => _worktrees[worktree] = (_worktrees[worktree].Head, true);

    /// <summary>Commits in <paramref name="worktree"/>: a new HEAD, clean.</summary>
    /// <param name="worktree">The path.</param>
    public void Advance(string worktree) => _worktrees[worktree] = (_worktrees[worktree].Head + "+", false);

    /// <inheritdoc/>
    public ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(_worktrees.TryGetValue(worktree, out var found) ? found.Head : Missing(worktree));

    /// <inheritdoc/>
    public ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(_worktrees.TryGetValue(worktree, out var found) ? !found.Dirty && string.Equals(found.Head, startHead, StringComparison.Ordinal) : Missing(worktree));

    private static Error Missing(string worktree) => WorkflowErrors.EffectFailed.With($"{worktree} is not a worktree");
}
