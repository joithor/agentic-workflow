using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>
/// At most one read in flight per pane (collector spec CA-9): a request that finds one running waits for its answer instead of starting a second. It is not a
/// cache: once a read is done the next request reads again.
/// </summary>
/// <param name="reader">Reads a pane.</param>
internal sealed class PaneCaptures(IPaneReader reader)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Socket, string Pane), Task<PaneRead>> _inFlight = [];

    /// <summary>The answer of the read of <paramref name="pane"/> that is running, or of a new one.</summary>
    /// <param name="socket">The server's socket path.</param>
    /// <param name="pane">The pane id.</param>
    /// <returns>The read.</returns>
    internal Task<PaneRead> Share(string socket, string pane)
    {
        var key = (socket, pane);
        lock (_gate)
        {
            if (_inFlight.TryGetValue(key, out var running))
            {
                return running;
            }

            var started = Task.Run(() => ReadAndForget(key));
            _inFlight[key] = started;
            return started;
        }
    }

    private PaneRead ReadAndForget((string Socket, string Pane) key)
    {
        try
        {
            return reader.Read(key.Socket, key.Pane, CancellationToken.None);   // bounded by the reader's own deadline; no caller's cancel may end a read others wait on
        }
        finally
        {
            lock (_gate)
            {
                _ = _inFlight.Remove(key);
            }
        }
    }
}
