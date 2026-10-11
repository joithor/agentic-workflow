using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// Which untracked files a clean work tree may hold: only what a run of the frozen check left behind in that same work
/// tree (a coverage file, a junit report), unchanged since, and never a file that could change what a run executes (a
/// protected file, or one a loader picks ahead of existing code). Anything else could be a planted fixture or module.
/// </summary>
public static class LeftoverRule
{
    /// <summary>The untracked files no accepted run left behind.</summary>
    /// <param name="checkout">The work tree, with its untracked files.</param>
    /// <param name="runs">The bug fix's registered run records.</param>
    /// <param name="check">The frozen check, once there is one.</param>
    /// <returns>The stray paths, sorted.</returns>
    public static IReadOnlyList<string> Stray(Checkout checkout, IEnumerable<RunRecord> runs, FrozenCheck? check)
    {
        var allowed = runs
            .Where(run => run.Checkout == checkout.Root && (check?.Ran(run) ?? (run.Kind == CheckKind.Test)))
            .SelectMany(static run => run.Leftovers.Select(l => (l.Key, l.Value)))
            .ToHashSet();
        return [.. checkout.Untracked
            .Where(f => !allowed.Contains((f.Key, f.Value)) || Loads(f.Key, check))
            .Select(static f => f.Key)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>What a run left behind: the untracked files after it that weren't there, or were different, before it.</summary>
    /// <param name="before">The untracked files before the run.</param>
    /// <param name="after">The untracked files after it.</param>
    /// <returns>The new or changed ones (path → SHA-256); files that can't be hashed are left out.</returns>
    public static IReadOnlyDictionary<string, string> Left(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
        after.Where(f => f.Value.Length > 0 && before.GetValueOrDefault(f.Key) != f.Value).ToDictionary(static f => f.Key, static f => f.Value, StringComparer.Ordinal);

    // A file that could change what a run executes is never harmless output, whoever wrote it.
    private static bool Loads(string path, FrozenCheck? check) =>
        ProtectedPaths.Touched([path], check?.Path ?? string.Empty, check?.CommandFiles ?? []).Count > 0
        || ProtectedPaths.Shadowing([path], check?.Directory ?? string.Empty, check?.Path ?? string.Empty).Count > 0;
}
