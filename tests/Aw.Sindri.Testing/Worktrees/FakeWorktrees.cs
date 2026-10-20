using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>The in-memory <see cref="IWorktrees"/>: repos with the commits they hold, and the worktrees made in them. Passes <see cref="WorktreesContract"/>.</summary>
public sealed class FakeWorktrees : IWorktrees
{
    private readonly Dictionary<string, HashSet<string>> _repos = [];
    private readonly Dictionary<(string Repo, string Branch), string> _branches = [];   // a branch's tip; it stays after its worktree goes, as in git
    private readonly Dictionary<string, (string Repo, WorktreeCheckout Checkout, bool Dirty)> _worktrees = [];
    private readonly Lock _gate = new();

    /// <summary>Tells the fake which commits a repo holds; a repo or sha it was not told about fails like git does.</summary>
    /// <param name="repoRoot">The repo's path.</param>
    /// <param name="commits">Its commit shas.</param>
    /// <returns>This fake.</returns>
    public FakeWorktrees WithRepo(string repoRoot, params string[] commits)
    {
        lock (_gate)
        {
            _repos[repoRoot] = [.. commits];
        }

        return this;
    }

    /// <summary>Leaves an uncommitted file in the worktree at <paramref name="path"/>.</summary>
    /// <param name="path">The worktree's path.</param>
    public void MakeDirty(string path)
    {
        lock (_gate)
        {
            _worktrees[path] = _worktrees[path] with { Dirty = true };
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            return ValueTask.FromResult(Make(request));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            if (!Path.IsPathRooted(path))
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.WorktreeInvalid.With("Path must be an absolute path"));
            }

            if (_worktrees.TryGetValue(path, out var found) && found.Dirty)
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.WorktreeDirty.With($"{path} has uncommitted changes"));
            }

            _ = _worktrees.Remove(path);
            return ValueTask.FromResult(Results.Ok());
        }
    }

    private Result<WorktreeCheckout> Make(WorktreeRequest request)
    {
        if (request.Validate() is { } invalid)
        {
            return invalid;
        }

        if (!_repos.TryGetValue(request.RepoRoot, out var commits) || !commits.Contains(request.BaseCommit))
        {
            return SindriErrors.WorktreeGitFailed.With($"{request.BaseCommit} is not a commit of {request.RepoRoot}");
        }

        if (_worktrees.TryGetValue(request.Path, out var existing))
        {
            return existing.Repo == request.RepoRoot && existing.Checkout.Branch == request.Branch && existing.Checkout.Head == request.BaseCommit
                ? existing.Checkout
                : SindriErrors.WorktreeConflict.With($"{request.Path} is already a worktree of {existing.Checkout.Branch} at {existing.Checkout.Head}");
        }

        if (Unusable(request) is { } problem)
        {
            return problem;
        }

        _branches[(request.RepoRoot, request.Branch)] = request.BaseCommit;
        var checkout = new WorktreeCheckout(request.Path, request.BaseCommit, request.Branch);
        _worktrees[request.Path] = (request.RepoRoot, checkout, false);
        return checkout;
    }

    private Error? Unusable(WorktreeRequest request)
    {
        if (_worktrees.Keys.Any(other => request.Path.StartsWith(other + "/", StringComparison.Ordinal)))
        {
            return SindriErrors.WorktreeInvalid.With($"{request.Path} is inside another worktree");
        }

        if (_worktrees.Values.Any(w => w.Repo == request.RepoRoot && w.Checkout.Branch == request.Branch))
        {
            return SindriErrors.WorktreeConflict.With($"{request.Branch} is checked out in another worktree");
        }

        return _branches.TryGetValue((request.RepoRoot, request.Branch), out var tip) && tip != request.BaseCommit
            ? SindriErrors.WorktreeConflict.With($"{request.Branch} exists at {tip}, not at {request.BaseCommit}")
            : null;
    }
}
