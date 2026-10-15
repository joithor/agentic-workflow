using Aw.Context.Core;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Map;

/// <summary>Where a written file may land: under the exports folder (links resolved), outside every git worktree.</summary>
internal static class ExportTarget
{
    /// <summary>The exports folder where it really is (links resolved).</summary>
    /// <param name="settings">The store settings.</param>
    /// <returns>The folder.</returns>
    public static string Folder(ContextStoreSettings settings)
    {
        var lexical = Path.GetFullPath(settings.ExportsDirectory);
        return RealPath.Of(lexical) ?? lexical;
    }

    /// <summary>Resolves the file a write goes to.</summary>
    /// <param name="settings">The store settings.</param>
    /// <param name="path">A file name or a path under the exports folder, or null.</param>
    /// <param name="defaultName">The name used when <paramref name="path"/> is empty.</param>
    /// <returns>The absolute path, SND-CONTEXT-005 inside a worktree, or SND-CONTEXT-033 outside the folder.</returns>
    public static Result<string> Resolve(ContextStoreSettings settings, string? path, string defaultName)
    {
        var lexical = Path.GetFullPath(settings.ExportsDirectory);
        var folder = Folder(settings);
        var named = string.IsNullOrEmpty(path) ? defaultName : path;
        if (named.Any(char.IsControl) || RealPath.Of(Path.GetFullPath(named, lexical)) is not { } target)
        {
            return ContextErrors.ExportOutsideHome.With($"the export path isn't resolvable or has control characters (fix: omit the path, or give a plain name under {folder})");
        }

        if (Worktrees.Containing(target) is { } worktree)
        {
            return ContextErrors.PathInWorktree.With($"{target} is inside the git worktree {worktree} (fix: omit the path, or give a name under {folder})");
        }

        return Inside(folder, target) ? target : ContextErrors.ExportOutsideHome.With($"{target} is outside {folder} (fix: omit the path, or give a name under it)");
    }

    /// <summary>Whether <paramref name="path"/> is strictly inside <paramref name="folder"/>.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="path">The path.</param>
    /// <returns>True when inside.</returns>
    public static bool Inside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        var first = relative.Split(Path.DirectorySeparatorChar)[0];
        return relative != "." && first != ".." && !Path.IsPathRooted(relative);
    }
}
