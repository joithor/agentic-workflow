using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>What host detection reads in one pass, each source under the pass's deadline. A source that fails or runs late is a problem row and an empty answer.</summary>
internal sealed class PassHostSources(SourcePass pass, IProcessTable processes, IUnixSockets sockets, ITmuxServers tmux, ITeamNames teams, ITerminalNames terminals) : IHostSources
{
    private static readonly IReadOnlyDictionary<string, string> _noEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _environments = [];
    private readonly Dictionary<int, string?> _socketOf = [];
    private readonly Dictionary<string, int> _serverOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TmuxServerView?> _views = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<long, string>? _sockets;

    public IReadOnlyList<TerminalName> Terminals => terminals.All;

    public IReadOnlyList<TeamNames> Teams => field ??= pass.Value("team-names", _ => teams.Read(), []);

    public IReadOnlyDictionary<string, string> Environment(int pid)
    {
        if (!_environments.TryGetValue(pid, out var environment))
        {
            environment = pass.Value("environment", pid, _ => processes.Environment(pid), _noEnvironment);
            _environments[pid] = environment;
        }

        return environment;
    }

    public string? SocketOf(int serverPid)
    {
        if (!_socketOf.TryGetValue(serverPid, out var path))
        {
            path = Find(serverPid);
            _socketOf[serverPid] = path;
            if (path is not null)
            {
                _serverOf[path] = serverPid;
            }
        }

        return path;
    }

    public TmuxServerView? View(string socket)
    {
        if (!_views.TryGetValue(socket, out var view))
        {
            view = Ask(socket);
            _views[socket] = view;
        }

        return view;
    }

    private string? Find(int serverPid)
    {
        _sockets ??= pass.Value("unix-sockets", _ => sockets.Read(), new Dictionary<long, string>());
        var files = pass.Value("open-files", serverPid, _ => processes.OpenFiles(serverPid), []);
        return files.Select(Inode).OfType<long>().Select(i => _sockets.GetValueOrDefault(i)).FirstOrDefault(static p => p is not null);
    }

    /// <summary>'socket:[12345]' is the link a socket file descriptor has; its number is the inode /proc/net/unix lists.</summary>
    private static long? Inode(string link) =>
        link.StartsWith("socket:[", StringComparison.Ordinal) && link.EndsWith(']') && long.TryParse(link.AsSpan("socket:[".Length, link.Length - "socket:[".Length - 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var inode) ? inode : null;

    private TmuxServerView? Ask(string socket)
    {
        var (outcome, result) = pass.Run("tmux", _serverOf.GetValueOrDefault(socket), token => tmux.Read(socket, token));
        if (outcome != SourceOutcome.Done)
        {
            pass.Note("tmux");
            return null;
        }

        return result is { IsSuccess: true } ok ? ok.Value : null;   // an unreachable server is not a problem row: the host is still 'tmux', with no place
    }
}
