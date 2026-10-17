namespace Aw.Sindri.Core.Sessions;

/// <summary>The walk up a process's parents and the names that make an ancestor a host (collector spec §6.1).</summary>
public static class HostChain
{
    /// <summary>At most this many parents are followed.</summary>
    public const int MaxSteps = 32;

    private const int CommLimit = 15;
    private static readonly string[] _warp = ["warp", "warp-terminal"];
    private static readonly string[] _ides = ["code", "cursor"];

    /// <summary>The parents of <paramref name="start"/>, nearest first: a missing parent, pid 0 or a cycle ends the walk.</summary>
    /// <param name="table">The process table by pid.</param>
    /// <param name="start">The process to start from; it is not in the result.</param>
    /// <returns>The ancestors.</returns>
    public static IReadOnlyList<ProcessEntry> Ancestors(IReadOnlyDictionary<int, ProcessEntry> table, ProcessEntry start)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(start);
        var chain = new List<ProcessEntry>();
        var seen = new HashSet<int> { start.Pid };
        var parent = start.ParentPid;
        while (chain.Count < MaxSteps && parent > 0 && seen.Add(parent) && table.TryGetValue(parent, out var next))
        {
            chain.Add(next);
            parent = next.ParentPid;
        }

        return chain;
    }

    /// <summary>The first ancestor that is a host, walking up.</summary>
    /// <param name="chain">The ancestors, nearest first.</param>
    /// <param name="terminals">The terminals known by process name.</param>
    /// <param name="allowTmux">False for an attached client's chain: a 'tmux: server' above a client is a nested tmux, not its terminal.</param>
    /// <returns>The hit, or null when none of the ancestors is a host.</returns>
    public static HostHit? Classify(IReadOnlyList<ProcessEntry> chain, IReadOnlyList<TerminalName> terminals, bool allowTmux)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(terminals);
        for (var i = 0; i < chain.Count; i++)
        {
            if (Kind(chain[i], terminals, allowTmux) is { } found)
            {
                return new HostHit(found.Kind, found.Name, chain[i], i == 0 ? null : chain[i - 1]);
            }
        }

        return null;
    }

    private static (HostKind Kind, string Name)? Kind(ProcessEntry process, IReadOnlyList<TerminalName> terminals, bool allowTmux)
    {
        var names = Names(process);
        if (allowTmux && (process.Comm.StartsWith("tmux: server", StringComparison.Ordinal) || process.Argv0?.StartsWith("tmux: server", StringComparison.Ordinal) == true))
        {
            return (HostKind.Tmux, "tmux");
        }

        if (names.Exists(static n => string.Equals(n, "sshd", StringComparison.Ordinal) || n.StartsWith("sshd-", StringComparison.Ordinal)))
        {
            return (HostKind.Ssh, "ssh");
        }

        if (terminals.FirstOrDefault(t => t.ProcessNames.Any(p => Matches(process, names, p))) is { } terminal)
        {
            return (HostKind.Terminal, terminal.Name);
        }

        if (names.Exists(static n => _warp.Contains(n, StringComparer.Ordinal)))
        {
            return (HostKind.Warp, "warp");
        }

        return names.Find(static n => _ides.Contains(n, StringComparer.Ordinal)) is { } ide ? (HostKind.Ide, ide) : null;
    }

    /// <summary>A process matches a name by comm, argv[0]'s or the exe's last segment; the kernel cuts comm at 15 characters, so a longer name also matches its 15-character start.</summary>
    private static bool Matches(ProcessEntry process, List<string> names, string name) =>
        names.Contains(name, StringComparer.Ordinal) || (name.Length > CommLimit && string.Equals(process.Comm, name[..CommLimit], StringComparison.Ordinal));

    private static List<string> Names(ProcessEntry process) =>
        [process.Comm, .. new[] { process.Argv0, process.ExePath }.OfType<string>().Select(Last)];

    private static string Last(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
