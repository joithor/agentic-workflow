using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// A run or a candidate must map to a commit: the work tree may have no uncommitted change, and no untracked file except
/// one a registered run left behind (its output) and nobody changed since. Any other untracked file (a new fixture, a
/// module that shadows another) could change what the check does while the commit stays the same.
/// </summary>
public static class TreeRule
{
    /// <summary>Refuses a dirty work tree.</summary>
    /// <param name="ports">The git and file ports and the run ledger.</param>
    /// <param name="state">The bug fix, whose registered runs say which leftovers are allowed.</param>
    /// <param name="directory">A directory in the work tree.</param>
    /// <param name="what">What the caller must do first ("commit the fix first").</param>
    /// <returns>Success, AW-BUGFIX-020, or AW-BUGFIX-018.</returns>
    public static Result<Unit> Clean(TreePorts ports, BugFixState state, string directory, string what)
    {
        var tracked = ports.Git.IsClean(directory);
        if (tracked.IsError || !tracked.Value)
        {
            return tracked.IsError ? tracked.Error : BugFixErrors.DirtyTree.With($"{directory} has uncommitted changes; {what}");
        }

        var allowed = Allowed(ports.Ledger, state);
        var stray = Leftovers(ports, directory).Map(found => found.Where(f => !allowed.Contains((f.Key, f.Value))).Select(static f => f.Key).Order(StringComparer.Ordinal).ToList());
        return stray.Bind<Unit>(paths => paths.Count == 0
            ? Unit.Value
            : BugFixErrors.DirtyTree.With($"{directory} has untracked files no registered run left behind ({string.Join(", ", paths.Take(5))}); remove or commit them; {what}"));
    }

    /// <summary>The untracked files of the work tree with their SHA-256: what a run leaves behind, recorded with it.</summary>
    /// <param name="ports">The git and file ports.</param>
    /// <param name="directory">A directory in the work tree.</param>
    /// <returns>Repository-relative path → SHA-256 (empty for a file that can't be read).</returns>
    public static Result<IReadOnlyDictionary<string, string>> Leftovers(TreePorts ports, string directory) =>
        ports.Git.Root(directory).Bind(root => ports.Git.Untracked(directory).Map(paths => (IReadOnlyDictionary<string, string>)paths.ToDictionary(
            static p => p,
            p => ports.Files.Sha256(ports.Files.FullPath(root, p)).Match(static s => s, static _ => string.Empty),
            StringComparer.Ordinal)));

    // Every (path, hash) a registered run of this bug fix left behind.
    private static HashSet<(string Path, string Sha256)> Allowed(IRunLedger ledger, BugFixState state)
    {
        var allowed = new HashSet<(string Path, string Sha256)>();
        foreach (var run in state.Runs)
        {
            var stored = ledger.Read(run.Evidence);
            if (stored.IsSuccess && stored.Value.Sha256 == run.Sha256)
            {
                allowed.UnionWith(stored.Value.Record.Leftovers.Where(static l => l.Value.Length > 0).Select(static l => (l.Key, l.Value)));
            }
        }

        return allowed;
    }
}
