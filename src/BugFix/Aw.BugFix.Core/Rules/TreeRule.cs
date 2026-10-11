using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>A run or a candidate must map to a commit: the work tree may have no uncommitted or untracked change.</summary>
public static class TreeRule
{
    /// <summary>Refuses a dirty work tree.</summary>
    /// <param name="git">The git port.</param>
    /// <param name="directory">A directory in the work tree.</param>
    /// <param name="what">What the caller must do first ("commit the fix first").</param>
    /// <returns>Success, AW-BUGFIX-020, or AW-BUGFIX-018.</returns>
    public static Result<Unit> Clean(IGit git, string directory, string what) =>
        git.IsClean(directory).Bind<Unit>(clean => clean
            ? Unit.Value
            : BugFixErrors.DirtyTree.With($"{directory} has uncommitted or untracked changes; {what}"));
}
