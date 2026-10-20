using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Worktrees;

/// <summary>
/// Worktrees through git ('git worktree add' and 'remove'). It never fetches, forces or resets: a branch that exists at the
/// base is reused, one at another commit is a conflict; a dirty tree is refused, not discarded. Create is idempotent through
/// 'git worktree list --porcelain': the path is already a worktree of that branch with HEAD at the base. Each git call is
/// bounded to 30 s.
/// </summary>
internal sealed class GitWorktrees : IWorktrees
{
    private const string HeadsPrefix = "refs/heads/";

    public async ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { } invalid)
        {
            return invalid;
        }

        var commit = await GitProcess.Run(request.RepoRoot, ["rev-parse", "--verify", "--quiet", request.BaseCommit + "^{commit}"], cancellationToken).ConfigureAwait(false);
        var listed = commit.Exit == 0 && commit.Output == request.BaseCommit
            ? await GitProcess.Run(request.RepoRoot, ["worktree", "list", "--porcelain"], cancellationToken).ConfigureAwait(false)
            : null;
        if (listed is not { Exit: 0 })
        {
            return SindriErrors.WorktreeGitFailed.With($"{request.BaseCommit} is not a commit of {request.RepoRoot}, or its worktrees can't be listed: {Reason(listed ?? commit)}");
        }

        var tip = await GitProcess.Run(request.RepoRoot, ["rev-parse", "--verify", "--quiet", HeadsPrefix + request.Branch], cancellationToken).ConfigureAwait(false);
        var worktrees = Parse(listed.Output);
        if (Existing(worktrees, request) is { } existing)
        {
            return existing;
        }

        if (Conflict(worktrees, request, tip.Exit == 0 ? tip.Output : null) is { } conflict)
        {
            return conflict;
        }

        return await Add(request, branchExists: tip.Exit == 0, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<WorktreeCheckout>> Add(WorktreeRequest request, bool branchExists, CancellationToken cancellationToken)
    {
        string[] add = branchExists
            ? ["worktree", "add", request.Path, request.Branch]
            : ["worktree", "add", "-b", request.Branch, request.Path, request.BaseCommit];
        var made = await GitProcess.Run(request.RepoRoot, add, cancellationToken).ConfigureAwait(false);
        return made.Exit == 0
            ? new WorktreeCheckout(request.Path, request.BaseCommit, request.Branch)
            : SindriErrors.WorktreeGitFailed.With($"git worktree add {request.Path} failed in {request.RepoRoot}: {Reason(made)}");
    }

    public async ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathRooted(path))
        {
            return SindriErrors.WorktreeInvalid.With("Path must be an absolute path");
        }

        if (!Directory.Exists(path))
        {
            return Results.Ok();   // gone already (an earlier remove, or a retried cleanup)
        }

        var (repository, problem) = await Inspect(path, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var removed = await GitProcess.Run(repository, ["worktree", "remove", path], cancellationToken).ConfigureAwait(false);
        if (removed.Exit != 0)
        {
            return removed.Error.Contains("modified or untracked", StringComparison.Ordinal)
                ? Dirty(path)
                : SindriErrors.WorktreeGitFailed.With($"git worktree remove {path} failed: {Reason(removed)}");
        }

        var pruned = await GitProcess.Run(repository, ["worktree", "prune"], cancellationToken).ConfigureAwait(false);
        return pruned.Exit == 0
            ? Results.Ok()
            : SindriErrors.WorktreeGitFailed.With($"git worktree prune failed in {repository}: {Reason(pruned)}");
    }

    /// <summary>The main checkout a worktree belongs to, or why it can't be removed: not a worktree, unreadable, or dirty.</summary>
    private static async Task<(string Repository, Error? Problem)> Inspect(string path, CancellationToken cancellationToken)
    {
        var common = await GitProcess.Run(path, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken).ConfigureAwait(false);
        var changes = common.Exit == 0 && common.Output.Length > 0
            ? await GitProcess.Run(path, ["status", "--porcelain"], cancellationToken).ConfigureAwait(false)
            : null;
        if (changes is not { Exit: 0 })
        {
            return (string.Empty, SindriErrors.WorktreeGitFailed.With($"{path} is not a git worktree, or its status can't be read: {Reason(changes ?? common)}"));
        }

        return (Path.GetDirectoryName(common.Output.TrimEnd('/')) ?? "/", changes.Output.Length > 0 ? Dirty(path) : null);
    }

    private static Error Dirty(string path) => SindriErrors.WorktreeDirty.With($"{path} has uncommitted or untracked files; it was left as it is");

    /// <summary>The worktree already at the request's path: the checkout when it is the one asked for, a conflict when it is not, null when the path is free.</summary>
    private static Result<WorktreeCheckout>? Existing(List<Listed> worktrees, WorktreeRequest request)
    {
        var path = Normal(request.Path);
        if (worktrees.FirstOrDefault(w => Normal(w.Path) == path) is not { } found)
        {
            return null;
        }

        return found.Branch == HeadsPrefix + request.Branch && found.Head == request.BaseCommit
            ? new WorktreeCheckout(request.Path, found.Head, request.Branch)
            : SindriErrors.WorktreeConflict.With($"{request.Path} is already a worktree of {found.Branch ?? "a detached HEAD"} at {found.Head}, not of {request.Branch} at {request.BaseCommit}");
    }

    /// <summary>Why a free path can't be used: it is inside another linked worktree, or the branch is checked out elsewhere or sits at another commit (nothing is reset). Null when it can be used.</summary>
    private static Error? Conflict(List<Listed> worktrees, WorktreeRequest request, string? branchTip)
    {
        var path = Normal(request.Path);
        // The first entry is the main checkout: the worktree may go inside it (<repo>/.claude/worktrees/…), but not inside another linked one.
        if (worktrees.Skip(1).FirstOrDefault(w => path.StartsWith(Normal(w.Path) + "/", StringComparison.Ordinal)) is { } outer)
        {
            return SindriErrors.WorktreeInvalid.With($"{request.Path} is inside the worktree {outer.Path}");
        }

        if (worktrees.FirstOrDefault(w => w.Branch == HeadsPrefix + request.Branch) is { } holder)
        {
            return SindriErrors.WorktreeConflict.With($"{request.Branch} is checked out in {holder.Path}");
        }

        return branchTip is not null && branchTip != request.BaseCommit
            ? SindriErrors.WorktreeConflict.With($"{request.Branch} exists at {branchTip}, not at {request.BaseCommit}; it is not reset")
            : null;
    }

    private static string Reason(GitRun run) => run.Error.Trim().Length > 0 ? run.Error.Trim() : "exit " + run.Exit.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Normal(string path) => (RealPath.Of(path) ?? path).TrimEnd('/');

    /// <summary>The entries of 'git worktree list --porcelain': blocks of 'worktree', 'HEAD' and 'branch' (or 'detached') lines separated by blank lines.</summary>
    private static List<Listed> Parse(string porcelain)
    {
        var entries = new List<Listed>();
        foreach (var block in porcelain.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? path = null, head = null, branch = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    path = line["worktree ".Length..];
                }
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                {
                    head = line["HEAD ".Length..];
                }
                else if (line.StartsWith("branch ", StringComparison.Ordinal))
                {
                    branch = line["branch ".Length..];
                }
            }

            if (path is not null)
            {
                entries.Add(new Listed(path, head ?? string.Empty, branch));
            }
        }

        return entries;
    }

    private sealed record Listed(string Path, string Head, string? Branch);
}
