using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// A run or a candidate must map to a commit: the work tree may have no uncommitted change, and no untracked file but
/// what <see cref="LeftoverRule"/> accepts.
/// </summary>
/// <param name="git">The git port.</param>
/// <param name="files">The file-system port.</param>
/// <param name="ledger">The run ledger, for the registered runs.</param>
public sealed class WorkTree(IGit git, IFiles files, IRunLedger ledger)
{
    /// <summary>The clean work tree <paramref name="directory"/> is in.</summary>
    /// <param name="state">The bug fix.</param>
    /// <param name="directory">A directory in the work tree.</param>
    /// <param name="what">What the caller must do first ("commit the fix first").</param>
    /// <returns>The work tree, AW-BUGFIX-020 when it isn't clean, or AW-BUGFIX-018.</returns>
    public Result<Checkout> Clean(BugFixState state, string directory, string what)
    {
        var tracked = git.IsClean(directory);
        if (tracked.IsError || !tracked.Value)
        {
            return tracked.IsError ? tracked.Error : BugFixErrors.DirtyTree.With($"{directory} has uncommitted changes; {what}");
        }

        var checkout = Snapshot(directory);
        var stray = checkout.Map(c => LeftoverRule.Stray(c, Registered(state), state.Check));
        return stray.Bind(paths => paths.Count == 0
            ? checkout
            : BugFixErrors.DirtyTree.With($"{directory} has untracked files no run of the frozen check left behind here ({string.Join(", ", paths.Take(5))}); remove or commit them; {what}"));
    }

    /// <summary>The work tree <paramref name="directory"/> is in, with its untracked files hashed, as it is now.</summary>
    /// <param name="directory">A directory in the work tree.</param>
    /// <returns>The work tree, or AW-BUGFIX-018.</returns>
    public Result<Checkout> Snapshot(string directory) =>
        git.Root(directory).Bind(root => git.RelativeDirectory(directory).Bind(relative => git.Head(directory).Bind(head => git.Untracked(directory).Map(paths =>
            new Checkout(root, relative, head, paths.ToDictionary(
                static p => p,
                p => files.Sha256(files.FullPath(root, p)).Match(static s => s, static _ => string.Empty),
                StringComparer.Ordinal))))));

    // The registered runs as recorded; a record changed since it was written doesn't count.
    private IEnumerable<RunRecord> Registered(BugFixState state) =>
        state.Runs.Select(run => ledger.Read(run.Evidence)).Where((stored, i) => stored.IsSuccess && stored.Value.Sha256 == state.Runs[i].Sha256).Select(static s => s.Value.Record);
}
