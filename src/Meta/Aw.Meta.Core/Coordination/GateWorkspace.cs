using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Coordination;

/// <summary>Builds the gate's worktrees: the pull request merged onto origin/main, and a plain-main tree for reruns.</summary>
/// <param name="repo">The git and gh calls.</param>
/// <param name="trees">The worktree handling.</param>
/// <param name="paths">Where the trees live.</param>
public sealed class GateWorkspace(RepoCommands repo, GateTree trees, MetaPaths paths)
{
    private const string Fields = "headRefOid,headRefName,isDraft,title";

    /// <summary>The gate tree of a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The directory.</returns>
    public string TreePath(int pr) => paths.GateDirectory(pr) + "/tree";

    /// <summary>The plain-main tree of a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The directory.</returns>
    public string MainTreePath(int pr) => paths.GateDirectory(pr) + "/main-tree";

    /// <summary>A step log's path: gates/&lt;pr&gt;/&lt;run&gt;/&lt;name&gt;.log.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="run">The gate run.</param>
    /// <param name="name">The step name.</param>
    /// <returns>The file path.</returns>
    public string LogPath(int pr, int run, string name) => string.Create(CultureInfo.InvariantCulture, $"{paths.GateDirectory(pr)}/{run}/{name}.log");

    /// <summary>The log of one rerun: gates/&lt;pr&gt;/&lt;run&gt;/rerun-&lt;step&gt;-&lt;test&gt;-&lt;where&gt;.log, the test name made safe for a file name. A later rerun of the same test appends to it.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="run">The gate run.</param>
    /// <param name="step">The step name.</param>
    /// <param name="test">The test filter.</param>
    /// <param name="where">"alone" (the gate tree) or "main" (the plain-main tree).</param>
    /// <returns>The file path.</returns>
    public string RerunLogPath(int pr, int run, string step, string test, string where)
    {
        ArgumentNullException.ThrowIfNull(test);
        var safe = new string([.. test.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').Take(80)]);
        return string.Create(CultureInfo.InvariantCulture, $"{paths.GateDirectory(pr)}/{run}/rerun-{step}-{safe}-{where}.log");
    }

    /// <summary>Fetches, checks origin/main out in the tree and merges the pull request head onto it.</summary>
    /// <param name="config">The team config (its repo).</param>
    /// <param name="pr">The pull request number.</param>
    /// <param name="listChanged">Whether to list the paths the pull request changes.</param>
    /// <param name="onto">For a train gate: the main to pin and the heads of the pull requests ahead of this one, merged in order before its own head; null for a plain gate on origin/main.</param>
    /// <param name="cancellationToken">Stops the git and gh calls (their process trees are killed) and throws the cancellation.</param>
    /// <returns>The prepared tree, or an error from git or gh.</returns>
    public ValueTask<Result<PreparedGate>> Prepare(TeamConfig config, int pr, bool listChanged, GateBase? onto = null, CancellationToken cancellationToken = default) =>
        cancellationToken.CanBeCanceled
            ? new GateWorkspace(repo.Cancelling(cancellationToken), trees.Cancelling(cancellationToken), paths).Prepared(config, pr, listChanged, onto)
            : Prepared(config, pr, listChanged, onto);

    private async ValueTask<Result<PreparedGate>> Prepared(TeamConfig config, int pr, bool listChanged, GateBase? onto)
    {
        var view = await repo.View(config.Repo, pr, Fields).ConfigureAwait(false);
        var main = view.IsError ? Results.Failure<string>(view.Error) : await Pin(config, pr, onto?.Main).ConfigureAwait(false);
        if (main.IsError)
        {
            return main.Error;
        }

        var stack = onto?.Stack ?? [];
        var built = await BuildTree(config, TreePath(pr), main.Value, [.. stack.Select(s => s.Head), view.Value.Head]).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var culprit = built.Value || stack.Count == 0 ? null : await Culprit(config.Repo, stack, view.Value.Head).ConfigureAwait(false);
        return await Describe(config.Repo, new PreparedGate(view.Value, main.Value, TreePath(pr), built.Value, []) { ConflictsWith = culprit }, listChanged).ConfigureAwait(false);
    }

    // Fetches the pull request, then resolves origin/main unless the train already pinned it.
    private async ValueTask<Result<string>> Pin(TeamConfig config, int pr, string? pinned)
    {
        var fetched = await repo.Fetch(config.Repo, pr).ConfigureAwait(false);
        return fetched.IsError ? fetched.Error : pinned ?? await repo.Sha(config.Repo, "origin/main").ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> BuildTree(TeamConfig config, string tree, string main, IReadOnlyList<string> heads)
    {
        var ready = await trees.Prepare(config.Repo, tree, main).ConfigureAwait(false);
        return ready.IsError ? ready.Error : await trees.MergeAll(tree, heads).ConfigureAwait(false);
    }

    // The stacked pull request whose head conflicts with this head on its own; the last one when none does alone (the conflict needs several).
    private async ValueTask<StackedPr> Culprit(string repoDirectory, IReadOnlyList<StackedPr> stack, string head)
    {
        foreach (var ahead in stack)
        {
            if (await repo.MergeTree(repoDirectory, ahead.Head, head).ConfigureAwait(false) is null)
            {
                return ahead;
            }
        }

        return stack[^1];
    }

    private async ValueTask<Result<PreparedGate>> Describe(string repoDirectory, PreparedGate prepared, bool listChanged)
    {
        var treeSha = await TreeSha(prepared.Tree, prepared.Merged).ConfigureAwait(false);
        var changed = treeSha.IsSuccess && listChanged && prepared.Merged
            ? await repo.Changed(repoDirectory, "origin/main..." + prepared.Pr.Head).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<string>>([]);
        return treeSha.IsError ? treeSha.Error : changed.IsError ? changed.Error : prepared with { Changed = changed.Value, TreeSha = treeSha.Value.Length == 0 ? null : treeSha.Value };
    }

    private async ValueTask<Result<string>> TreeSha(string tree, bool merged) =>
        merged ? await repo.Git(tree, "rev-parse", "HEAD^{tree}").ConfigureAwait(false) : string.Empty;

    /// <summary>Checks the plain-main tree out at the main the gate ran on, so a failure there predates the pull request.</summary>
    /// <param name="config">The team config (its repo).</param>
    /// <param name="pr">The pull request number.</param>
    /// <param name="main">The sha to check out.</param>
    /// <returns>The tree's directory, or an error.</returns>
    public async ValueTask<Result<string>> PrepareMain(TeamConfig config, int pr, string main)
    {
        var tree = MainTreePath(pr);
        var ready = await trees.Prepare(config.Repo, tree, main).ConfigureAwait(false);
        return ready.IsError ? ready.Error : tree;
    }
}
