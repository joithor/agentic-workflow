using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// Finds the check file and insists it is committed: candidate worktrees start from the baseline commit, so an
/// untracked or modified check would not be what they run.
/// </summary>
/// <param name="git">The git port.</param>
/// <param name="files">The file-system port.</param>
public sealed class CheckLocator(IGit git, IFiles files)
{
    /// <summary>Locates <paramref name="path"/> (relative to <paramref name="directory"/>) inside its repository.</summary>
    /// <param name="directory">The working directory the path is relative to.</param>
    /// <param name="path">The check path.</param>
    /// <returns>The committed check, or AW-BUGFIX-008 (or AW-BUGFIX-018 when git fails).</returns>
    public Result<CheckFile> Locate(string directory, string path)
    {
        var full = files.FullPath(directory, path);
        if (files.RealPath(full) is not { } real)
        {
            return BugFixErrors.CheckNotFrozen.With($"check file not found: {full}");
        }

        return git.Root(directory).Bind(root => Committed(root, real));
    }

    private Result<CheckFile> Committed(string root, string real)
    {
        var prefix = root.EndsWith('/') ? root : root + "/";
        if (!real.StartsWith(prefix, StringComparison.Ordinal))
        {
            return BugFixErrors.CheckNotFrozen.With($"check file {real} is outside the repo {root}");
        }

        var relative = real[prefix.Length..];
        return git.IsCommittedUnchanged(root, relative).Bind<CheckFile>(committed => committed
            ? new CheckFile(relative, real)
            : BugFixErrors.CheckNotFrozen.With($"check file {relative} is not committed as it is on disk; commit it on the bugfix branch first"));
    }
}
