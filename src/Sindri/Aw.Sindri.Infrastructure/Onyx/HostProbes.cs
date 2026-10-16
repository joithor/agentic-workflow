using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IHostProbes over the file system and loopback sockets: DriveInfo of the nearest existing ancestor, and a bind-and-release per port.</summary>
internal sealed class HostProbes : IHostProbes
{
    /// <inheritdoc />
    public DiskFacts FreeDisk(string path)
    {
        var measured = Path.GetFullPath(path);
        while (!Directory.Exists(measured) && Path.GetDirectoryName(measured) is { Length: > 0 } parent)
        {
            measured = parent;
        }

        return new DiskFacts(measured, new DriveInfo(measured).AvailableFreeSpace);
    }

    /// <inheritdoc />
    public PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast)
    {
        int? fallback = null;
        for (var port = fallbackFirst; port <= fallbackLast && fallback is null; port++)
        {
            fallback = IsFree(port) ? port : null;
        }

        return new PortFacts(preferred, IsFree(preferred), fallback);
    }

    // Free means nothing answers on 127.0.0.1:<port> and it can be bound. The connect matters: on BSD and macOS a bind to
    // 127.0.0.1 can succeed beside another process listening on 0.0.0.0 (what Docker publishes), so a bind alone would call that port free.
    private static bool IsFree(int port) => !Answers(port) && CanBind(port);

    private static bool Answers(int port)
    {
        using var client = new TcpClient();
        try
        {
            return client.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch (AggregateException)
        {
            return false;   // refused: nothing listens
        }
    }

    private static bool CanBind(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}
