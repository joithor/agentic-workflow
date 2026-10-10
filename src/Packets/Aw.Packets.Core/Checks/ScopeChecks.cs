using System.Collections;
using System.Globalization;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The rules that read the base commit: the base sha, write scopes that exist, and overlapping write scopes.</summary>
internal static class ScopeChecks
{
    private const string ScopeFix =
        "Point the scope at files that exist at the base commit (a relative glob), or declare a file the task creates as 'new: <path>'.";

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

        var parsed = new Dictionary<string, ScopeGlob?>(StringComparer.Ordinal);   // one parse per distinct entry
        var scopes = packet.Tasks.Tasks.ToDictionary(
            static t => t,
            t => t.WriteScope.Select(e => (e, parsed.TryGetValue(e, out var known) ? known : parsed[e] = ScopeGlob.Parse(e))).ToList());
        var index = new PathIndex(files, parsed.Values.Select(static g => g?.NewPath).OfType<string>());
        checks.Rule(PacketRules.ScopeExists, "every write scope exists at the base commit or is declared new", Missing(scopes, index, sha));
        checks.Rule(PacketRules.ScopeOverlap, "no two unordered tasks share a write scope", Overlaps(packet, scopes, index));
    }

    private static IEnumerable<(string, string, string)> Missing(
        Dictionary<PacketTask, List<(string Entry, ScopeGlob? Glob)>> scopes, PathIndex index, string sha)
    {
        foreach (var (task, entries) in scopes)
        {
            foreach (var (entry, glob) in entries)
            {
                if (Problem(entry, glob, index, sha) is { } problem)
                {
                    yield return ($"task {task.Id.Value}", $"write scope '{Shown(entry)}' {problem}", ScopeFix);
                }
            }
        }
    }

    private static string? Problem(string entry, ScopeGlob? glob, PathIndex index, string sha) => glob switch
    {
        null => $"isn't a glob or a safe 'new: <relative path>' of at most {ScopeGlob.MaxLength} characters",
        { NewPath: { } path } when index.IsFile(path) => $"declares '{path}' new, but it exists at {Short(sha)}",
        { NewPath: { } path } when index.IsDirectory(path) => $"declares '{path}' new, but it is a directory (at {Short(sha)} or above a declared new path)",
        { NewPath: { } path } when index.FileAbove(path) is { } file => $"declares '{path}' new, but '{file}' is a file (at {Short(sha)} or declared new)",
        { NewPath: null } when entry.TrimStart().StartsWith('/') => "starts with '/': write scopes are relative to the repository root",
        { NewPath: null } when entry.Trim().Split('/').Any(static s => string.Equals(s, ".", StringComparison.Ordinal) || string.Equals(s, "..", StringComparison.Ordinal)) => "has a '.' or '..' segment: write scopes are relative to the repository root, with no dot segments",
        { NewPath: null } when !index.MatchesAny(glob) => $"matches no file at {Short(sha)}",
        _ => null,
    };

    private static IEnumerable<(string, string, string)> Overlaps(
        Packet packet, Dictionary<PacketTask, List<(string Entry, ScopeGlob? Glob)>> scopes, PathIndex index)
    {
        var graph = new TaskGraph(packet.Tasks.Tasks);
        var tasks = packet.Tasks.Tasks.OrderBy(static t => t.Id.Value, StringComparer.Ordinal).ToList();
        var writes = tasks.ToDictionary(static t => t, t => PathIndex.Words(Written(scopes[t], index)));
        for (var i = 0; i < tasks.Count; i++)
        {
            for (var j = i + 1; j < tasks.Count; j++)
            {
                var (a, b) = (tasks[i], tasks[j]);
                // the shared path first: reachability is only computed for tasks that share one
                if (a.Id != b.Id && index.FirstShared(writes[a], writes[b]) is { } path && !graph.Ordered(a.Id, b.Id))
                {
                    yield return ($"tasks {a.Id.Value}, {b.Id.Value}",
                        $"tasks {a.Id.Value} and {b.Id.Value} can run at the same time and both write '{path}'",
                        $"Make one depend on the other, or split the write scopes so they don't share '{path}'.");
                }
            }
        }
    }

    // The paths a task's scope matches: the union of its entries' bits.
    private static BitArray Written(List<(string Entry, ScopeGlob? Glob)> entries, PathIndex index)
    {
        var bits = new BitArray(index.Paths.Count);
        foreach (var glob in entries.Select(static e => e.Glob).OfType<ScopeGlob>())
        {
            index.MatchInto(glob, bits);
        }

        return bits;
    }

    // An entry as a message shows it: a long one is cut, with its length.
    private static string Shown(string entry) =>
        entry.Length <= 80 ? entry : string.Create(CultureInfo.InvariantCulture, $"{entry[..80]}… ({entry.Length} characters)");

    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;
}
