using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.GateRun;

/// <summary>Builds the gate's worktrees: the pull request merged onto origin/main, and a plain-main tree for reruns.</summary>
/// <param name="repo">The git and gh calls.</param>
/// <param name="trees">The worktree handling.</param>
/// <param name="paths">Where the trees live.</param>
internal sealed class GateWorkspace(RepoCommands repo, GateTree trees, MetaPaths paths)
{
    private const string Fields = "headRefOid,headRefName,isDraft,title";

    internal string TreePath(int pr) => paths.GateDirectory(pr) + "/tree";

    internal string MainTreePath(int pr) => paths.GateDirectory(pr) + "/main-tree";

    internal string LogPath(int pr, int run, string name) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{paths.GateDirectory(pr)}/{run}/{name}.log");

    // Fetches, checks origin/main out in the tree and merges the head onto it.
    internal async ValueTask<Result<PreparedGate>> Prepare(TeamConfig config, int pr, bool listChanged)
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

        var changed = listChanged && merged.Value
            ? await repo.Changed(config.Repo, "origin/main..." + view.Value.Head).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<string>>([]);
        return changed.IsError ? changed.Error : new PreparedGate(view.Value, main.Value, tree, merged.Value, changed.Value);
    }

    // The plain-main tree for a rerun: the main the gate ran on, so a failure there predates the pull request.
    internal async ValueTask<Result<string>> PrepareMain(TeamConfig config, int pr, string main)
    {
        var tree = MainTreePath(pr);
        var ready = await trees.Prepare(config.Repo, tree, main).ConfigureAwait(false);
        return ready.IsError ? ready.Error : tree;
    }
}
