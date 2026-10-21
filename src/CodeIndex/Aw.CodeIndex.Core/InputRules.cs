using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>
/// What the snapshot keeps of a commit's tree (spec §5.1, §9): regular files only, with a safe path, outside the deny globs,
/// under the size caps. Pure, so the git adapter and the fake apply the same rules.
/// </summary>
public static class InputRules
{
    /// <summary>The secret globs every repo has; <see cref="SnapshotLimits.DenyPaths"/> only adds to them.</summary>
    public static IReadOnlyList<string> SecretGlobs { get; } = [];

    /// <summary>Whether every component of a path is safe to write under a snapshot root.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>False for an empty, '..' or '.git' (any case) component, a backslash, a NUL, or an absolute path.</returns>
    public static bool IsSafePath(string path) => throw new NotSupportedException();

    /// <summary>Whether a path matches a built-in secret glob or one of the added ones, ignoring case.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="addedGlobs">The profile's and the repo's globs.</param>
    /// <returns>True when the path is denied.</returns>
    public static bool IsDenied(string path, IReadOnlyList<string> addedGlobs) => throw new NotSupportedException();

    /// <summary>Applies the rules to a commit's tree.</summary>
    /// <param name="commit">The commit the tree belongs to.</param>
    /// <param name="entries">Every blob and submodule entry, as git lists them.</param>
    /// <param name="limits">The deny globs and caps.</param>
    /// <returns>The kept files in path order and a count per skip reason; SND-CODEIDX-003 when the kept sizes or the kept file count pass their cap.</returns>
    public static Result<SnapshotInventory> Apply(CommitSha commit, IEnumerable<TreeEntry> entries, SnapshotLimits limits) => throw new NotSupportedException();
}
