using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;

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
    /// <returns>The prepared tree, or an error from git or gh.</returns>
    public async ValueTask<Result<PreparedGate>> Prepare(TeamConfig config, int pr, bool listChanged)
    {
        var view = await repo.View(config.Repo, pr, Fields).ConfigureAwait(false);
        var fetched = view.IsError ? Results.Failure<Unit>(view.Error) : await repo.Fetch(config.Repo, pr).ConfigureAwait(false);
        var main = fetched.IsError ? Results.Failure<string>(fetched.Error) : await repo.Sha(config.Repo, "origin/main").ConfigureAwait(false);
        if (main.IsError)
        {
            return main.Error;
        }

        var tree = TreePath(pr);
        var ready = await trees.Prepare(config.Repo, tree, main.Value).ConfigureAwait(false);
        var merged = ready.IsError ? Results.Failure<bool>(ready.Error) : await trees.Merge(tree, view.Value.Head).ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        return await Describe(config.Repo, new PreparedGate(view.Value, main.Value, tree, merged.Value, []), listChanged).ConfigureAwait(false);
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
