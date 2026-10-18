using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>What host detection asks the world for, as dictionaries: environments by pid, sockets by server pid, views by socket.</summary>
internal sealed class FakeHostSources : IHostSources
{
    public IReadOnlyList<TerminalName> Terminals { get; init; } = new FakeTerminalNames().All;

    public IReadOnlyList<TeamNames> Teams { get; init; } = [];

    public Dictionary<int, IReadOnlyDictionary<string, string>> Environments { get; } = [];

    public Dictionary<int, string> Sockets { get; } = [];

    public Dictionary<string, TmuxServerView> Views { get; } = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Environment(int pid) => Environments.GetValueOrDefault(pid) ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public string? SocketOf(int serverPid) => Sockets.GetValueOrDefault(serverPid);

    public TmuxServerView? View(string socket) => Views.GetValueOrDefault(socket);
}
