using System.Text;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Panes by socket and id, in memory; anything else is gone. It counts reads and can hold one until a test lets it go. Kept honest by the pane reader contract.</summary>
internal sealed class FakePaneReader : IPaneReader
{
    private readonly Dictionary<(string Socket, string Pane), PaneRead> _panes = [];
    private readonly HashSet<string> _slow = new(StringComparer.Ordinal);
    private int _reads;

    /// <summary>Releases a read that is held; null reads straight through.</summary>
    public ManualResetEventSlim? Hold { get; set; }

    /// <summary>Signalled each time a read starts.</summary>
    public SemaphoreSlim Started { get; } = new(0);

    public int Reads => Volatile.Read(ref _reads);

    public FakePaneReader With(string socket, string pane, int panePid, string text) => WithBytes(socket, pane, panePid, Encoding.UTF8.GetBytes(text));

    /// <summary>Adds the pane unless a test has already set it.</summary>
    public FakePaneReader WithDefault(string socket, string pane, int panePid, string text) => _panes.ContainsKey((socket, pane)) ? this : With(socket, pane, panePid, text);

    public FakePaneReader WithBytes(string socket, string pane, int panePid, byte[] capture)
    {
        _panes[(socket, pane)] = PaneRead.Captured(panePid, capture);
        return this;
    }

    /// <summary>Makes every read on <paramref name="socket"/> answer TimedOut.</summary>
    public FakePaneReader TimingOut(string socket)
    {
        _ = _slow.Add(socket);
        return this;
    }

    public PaneRead Read(string socket, string pane, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _reads);
        _ = Started.Release();
        Hold?.Wait(CancellationToken.None);
        if (!Path.IsPathRooted(socket) || !IsPaneId(pane))
        {
            return PaneRead.Gone;
        }

        return _slow.Contains(socket) ? PaneRead.TimedOut : _panes.GetValueOrDefault((socket, pane)) ?? PaneRead.Gone;
    }

    private static bool IsPaneId(string pane) => pane.Length > 1 && pane[0] == '%' && pane.Skip(1).All(char.IsAsciiDigit);
}
