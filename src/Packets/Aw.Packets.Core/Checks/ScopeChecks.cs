using System.Globalization;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The rules that read the base commit: the base sha, write scopes that exist, and overlapping write scopes.</summary>
internal static class ScopeChecks
{
    internal static void Run(Packet packet, BaseTree tree, CheckList checks)
    {
        var sha = packet.Manifest.BaseSha;
        checks.Rule(
            PacketRules.BaseCommit,
            $"base commit {Short(sha)} is in the repository",
            tree.Problem is { } problem ? [("packet", problem, "Set baseSha to a full commit sha that exists in the repository.")] : []);
        if (tree.Files is not { } files)
        {
            const string Reason = "skipped: the base commit can't be read (PKT-BASE-001)";
            checks.Unchecked(PacketRules.ScopeExists, Reason);
            checks.Unchecked(PacketRules.ScopeOverlap, Reason);
            return;
        }

        var scopes = packet.Tasks.Tasks.ToDictionary(static t => t, static t => t.WriteScope.Select(e => (e, ScopeGlob.Parse(e))).ToList());
        checks.Rule(PacketRules.ScopeExists, "every write scope exists at the base commit or is declared new", Missing(scopes, files, sha));
        checks.Rule(PacketRules.ScopeOverlap, "no two unordered tasks share a write scope", Overlaps(packet, scopes, files));
    }

    private static IEnumerable<(string, string, string)> Missing(
        Dictionary<PacketTask, List<(string Entry, ScopeGlob? Glob)>> scopes, IReadOnlySet<string> files, string sha)
    {
        foreach (var (task, entries) in scopes)
        {
            foreach (var (entry, glob) in entries)
            {
                var problem = glob switch
                {
                    null => $"isn't a glob or a safe 'new: <relative path>' of at most {ScopeGlob.MaxLength} characters",
                    { NewPath: { } path } when files.Contains(path) => $"declares '{path}' new, but it exists at {Short(sha)}",
                    { NewPath: null } when !files.Any(glob.Matches) => $"matches no file at {Short(sha)}",
                    _ => null,
                };
                if (problem is not null)
                {
                    yield return ($"task {task.Id.Value}", $"write scope '{Shown(entry)}' {problem}",
                        "Point the scope at files that exist at the base commit, or declare a file the task creates as 'new: <path>'.");
                }
            }
        }
    }

    private static IEnumerable<(string, string, string)> Overlaps(
        Packet packet, Dictionary<PacketTask, List<(string Entry, ScopeGlob? Glob)>> scopes, IReadOnlySet<string> files)
    {
        var graph = new TaskGraph(packet.Tasks.Tasks);
        var tasks = packet.Tasks.Tasks.OrderBy(static t => t.Id.Value, StringComparer.Ordinal).ToList();
        var declared = scopes.Values.SelectMany(static e => e).Select(static e => e.Glob?.NewPath).OfType<string>();
        var paths = files.Concat(declared).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var writes = tasks.ToDictionary(static t => t, t => Written(Globs(scopes[t]), paths));
        for (var i = 0; i < tasks.Count; i++)
        {
            for (var j = i + 1; j < tasks.Count; j++)
            {
                var (a, b) = (tasks[i], tasks[j]);
                // the shared path first: reachability is only computed for tasks that share one
                if (a.Id != b.Id && writes[a].FirstOrDefault(writes[b].Contains) is { } path && !graph.Ordered(a.Id, b.Id))
                {
                    yield return ($"tasks {a.Id.Value}, {b.Id.Value}",
                        $"tasks {a.Id.Value} and {b.Id.Value} can run at the same time and both write '{path}'",
                        $"Make one depend on the other, or split the write scopes so they don't share '{path}'.");
                }
            }
        }
    }

    private static List<ScopeGlob> Globs(List<(string Entry, ScopeGlob? Glob)> entries) =>
        [.. entries.Select(static e => e.Glob).OfType<ScopeGlob>()];

    // The paths (sorted by ordinal) a task's scope matches: base files and every declared new path.
    private static SortedSet<string> Written(List<ScopeGlob> globs, List<string> paths) =>
        new(paths.Where(p => globs.Exists(g => g.Matches(p))), StringComparer.Ordinal);

    // An entry as a message shows it: a long one is cut, with its length.
    private static string Shown(string entry) =>
        entry.Length <= 80 ? entry : string.Create(CultureInfo.InvariantCulture, $"{entry[..80]}… ({entry.Length} characters)");

    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;
}
