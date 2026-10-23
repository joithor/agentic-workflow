using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// An <see cref="IWorkspaceRebase"/> in memory: the branch tips Fetch answers, the ancestry Contains reports and the result Rebase gives are what a test set; it records every call. A branch
/// it was not told about is an error.
/// </summary>
public sealed class FakeWorkspaceRebase : IWorkspaceRebase
{
    private readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal);
    private readonly HashSet<(string Ancestor, string Descendant)> _ancestry = [];

    /// <summary>Gets the branches fetched, in order.</summary>
    public IList<string> Fetched { get; } = [];

    /// <summary>Gets the rebases asked for (onto, from), in order.</summary>
    public IList<(string Onto, string? From)> Rebases { get; } = [];

    /// <summary>Gets the commits the workspace was reset to, in order.</summary>
    public IList<string> Resets { get; } = [];

    /// <summary>Gets or sets what Rebase answers.</summary>
    public Result<WorkspaceRebased> RebaseAnswer { get; set; } = new WorkspaceRebased("rebased-head");

    /// <summary>Makes <paramref name="branch"/> point at <paramref name="sha"/> on the fake remote.</summary>
    /// <param name="branch">The branch.</param>
    /// <param name="sha">Its tip.</param>
    /// <returns>The same instance.</returns>
    public FakeWorkspaceRebase Tip(string branch, string sha)
    {
        _tips[branch] = sha;
        return this;
    }

    /// <summary>Says <paramref name="ancestor"/> is in the history of <paramref name="descendant"/>.</summary>
    /// <param name="ancestor">The older commit.</param>
    /// <param name="descendant">The newer commit.</param>
    /// <returns>The same instance.</returns>
    public FakeWorkspaceRebase Descends(string ancestor, string descendant)
    {
        _ = _ancestry.Add((ancestor, descendant));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Fetch(string worktree, string branch, CancellationToken cancellationToken)
    {
        Fetched.Add(branch);
        return ValueTask.FromResult<Result<string>>(_tips.TryGetValue(branch, out var sha) ? sha : WorkflowErrors.EffectFailed.With($"origin has no branch '{branch}'"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Contains(string worktree, string ancestor, string descendant, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(string.Equals(ancestor, descendant, StringComparison.Ordinal) || _ancestry.Contains((ancestor, descendant)));

    /// <inheritdoc/>
    public ValueTask<Result<WorkspaceRebased>> Rebase(string worktree, string onto, string? from, CancellationToken cancellationToken)
    {
        Rebases.Add((onto, from));
        return ValueTask.FromResult(RebaseAnswer);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Reset(string worktree, string sha, CancellationToken cancellationToken)
    {
        Resets.Add(sha);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
